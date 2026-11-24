using System.IO;
using System.Windows.Media.Imaging;

namespace DesktopNotes.Services;

internal sealed record ImagePreview(BitmapSource Bitmap, int PixelWidth, int PixelHeight, double Width, double Height);

/// <summary>Shared lazy LRU cache. Controls may independently retain active thumbnails.</summary>
public static class ImageDecodeService
{
    private const long CacheBudget = 2 * 1024 * 1024;
    public const int MaxThumbnailEdge = 1024;
    public const int MaxNotePreviewWidth = 512;
    private static readonly object Gate = new();
    private static readonly LinkedList<(string Key, BitmapSource Bitmap, long Bytes)> Cache = new();
    private static long _bytes;
    internal static readonly SemaphoreSlim Worker = new(1);

    public static async Task<BitmapSource> LoadAsync(string path, int pixelWidth, CancellationToken cancellationToken)
    {
        string key = Path.GetFullPath(path) + ":" + new FileInfo(path).LastWriteTimeUtc.Ticks + ":" + pixelWidth;
        cancellationToken.ThrowIfCancellationRequested();
        if (pixelWidth > 0 && FindCached(key) is { } cached) return cached;
        await Worker.WaitAsync(cancellationToken);
        try
        {
            if (pixelWidth > 0 && FindCached(key) is { } queued) return queued;
            var image = await Task.Run(() => Decode(path, pixelWidth), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            long bytes = (long)image.PixelWidth * image.PixelHeight * Math.Max(4, (image.Format.BitsPerPixel + 7) / 8);
            if (pixelWidth > 0 && bytes <= CacheBudget)
            {
                lock (Gate)
                {
                    Cache.AddFirst((key, image, bytes)); _bytes += bytes;
                    while (_bytes > CacheBudget && Cache.Last != null)
                    { _bytes -= Cache.Last.Value.Bytes; Cache.RemoveLast(); }
                }
            }
            return image;
        }
        finally { Worker.Release(); }
    }

    private static BitmapSource? FindCached(string key)
    {
        lock (Gate)
        {
            for (var node = Cache.First; node != null; node = node.Next)
            {
                if (node.Value.Key != key) continue;
                Cache.Remove(node);
                Cache.AddFirst(node);
                return node.Value.Bitmap;
            }
        }
        return null;
    }

    public static int NotePreviewWidth(double displayPixels)
    {
        // Smaller thumbnails can tolerate slight upscaling. Narrow buckets keep
        // them from jumping to the next large decode size for a few pixels.
        if (displayPixels <= 192)
            return Math.Clamp((int)Math.Ceiling(displayPixels * .8 / 16) * 16, 32, 160);
        return Math.Min((int)Math.Ceiling(displayPixels / 32) * 32, MaxNotePreviewWidth);
    }

    internal static async Task<ImagePreview> LoadViewerPreviewAsync(string path, CancellationToken cancellationToken)
    {
        await Worker.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                using var input = File.OpenRead(path);
                var original = ReadFrame(input);
                int width = original.PixelWidth;
                int height = original.PixelHeight;
                double layoutWidth = original.Width;
                double layoutHeight = original.Height;
                return new ImagePreview(Decode(input, original, MaxThumbnailEdge), width, height, layoutWidth, layoutHeight);
            }, cancellationToken);
        }
        finally { Worker.Release(); }
    }

    public static BitmapSource Decode(string path, int width = 0)
    {
        using var input = File.OpenRead(path);
        return Decode(input, ReadFrame(input), width);
    }

    private static BitmapFrame ReadFrame(Stream input)
    {
        var header = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = header.Frames[0];
        ImageAssetStore.ValidateSize(frame);
        return frame;
    }

    private static BitmapSource Decode(Stream input, BitmapFrame original, int width)
    {
        input.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = input;
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0)
        {
            int targetWidth = Math.Min(Math.Min(width, MaxThumbnailEdge), original.PixelWidth);
            if ((long)original.PixelHeight * targetWidth > (long)MaxThumbnailEdge * original.PixelWidth)
                image.DecodePixelHeight = MaxThumbnailEdge;
            else
                image.DecodePixelWidth = targetWidth;
        }
        image.EndInit(); image.Freeze();
        return image;
    }

    public static void Clear() { lock (Gate) { Cache.Clear(); _bytes = 0; } }
}
