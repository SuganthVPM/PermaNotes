using System.IO;
using System.Windows.Media.Imaging;

namespace DesktopNotes.Services;

/// <summary>Shared lazy LRU cache. Controls may independently retain active thumbnails.</summary>
public static class ImageDecodeService
{
    private const long CacheBudget = 16 * 1024 * 1024;
    private static readonly object Gate = new();
    private static readonly LinkedList<(string Key, BitmapSource Bitmap, long Bytes)> Cache = new();
    private static long _bytes;
    internal static readonly SemaphoreSlim Worker = new(1);

    public static async Task<BitmapSource> LoadAsync(string path, int pixelWidth, CancellationToken cancellationToken)
    {
        string key = Path.GetFullPath(path) + ":" + new FileInfo(path).LastWriteTimeUtc.Ticks + ":" + pixelWidth;
        lock (Gate)
        {
            var node = Cache.First;
            while (node != null)
            {
                if (node.Value.Key == key)
                {
                    Cache.Remove(node); Cache.AddFirst(node);
                    return node.Value.Bitmap;
                }
                node = node.Next;
            }
        }
        await Worker.WaitAsync(cancellationToken);
        try
        {
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

    public static BitmapSource Decode(string path, int width = 0)
    {
        using var input = File.OpenRead(path);
        var header = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        ImageAssetStore.ValidateSize(header.Frames[0]);
        input.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = input;
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0) image.DecodePixelWidth = Math.Min(width, header.Frames[0].PixelWidth);
        image.EndInit(); image.Freeze();
        return image;
    }

    public static void Clear() { lock (Gate) { Cache.Clear(); _bytes = 0; } }
}
