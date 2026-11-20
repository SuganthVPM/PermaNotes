using System.IO;
using System.Windows.Media.Imaging;
using DesktopNotes.Models;

namespace DesktopNotes.Services;

/// <summary>Immutable, per-note image assets. Removing editor content never deletes assets.</summary>
public sealed class ImageAssetStore
{
    public const long MaximumFileBytes = 25L * 1024 * 1024;
    public const long MaximumPixels = 40_000_000;
    private static int _pendingImports;
    public static bool HasPendingImports => Volatile.Read(ref _pendingImports) > 0;
    private readonly string _root;
    public string StorageRoot => _root;
    public ImageAssetStore(string storageRoot) => _root = Path.GetFullPath(storageRoot);

    public string DirectoryFor(Guid noteId) => Path.Combine(_root, "Images", noteId.ToString("N"));

    public async Task<NoteImageReference> ImportAsync(Guid noteId, string file)
    {
        Interlocked.Increment(ref _pendingImports);
        await ImageDecodeService.Worker.WaitAsync();
        try { return await Task.Run(() => Import(noteId, file)); }
        finally { ImageDecodeService.Worker.Release(); Interlocked.Decrement(ref _pendingImports); }
    }
    public async Task<NoteImageReference> ImportAsync(Guid noteId, BitmapSource bitmap)
    {
        Interlocked.Increment(ref _pendingImports);
        await ImageDecodeService.Worker.WaitAsync();
        try { return await Task.Run(() => Import(noteId, bitmap)); }
        finally { ImageDecodeService.Worker.Release(); Interlocked.Decrement(ref _pendingImports); }
    }

    public string Resolve(Guid noteId, string fileName)
    {
        if (!NoteImageReference.IsSafeFileName(fileName)) throw new InvalidDataException("Invalid image filename.");
        string current = Path.Combine(DirectoryFor(noteId), fileName);
        string legacy = Path.Combine(_root, "Images", NoteStorageService.GetShortId(noteId), fileName);
        if (!File.Exists(current) && !File.Exists(legacy))
        {
            foreach (string folder in new[] { noteId.ToString("N"), NoteStorageService.GetShortId(noteId) })
            {
                string quarantined = Path.Combine(_root, "Trash", "Assets", folder, fileName);
                if (File.Exists(quarantined))
                { AtomicFile.Write(current, output => { using var input = File.OpenRead(quarantined); input.CopyTo(output); }); break; }
            }
        }
        return File.Exists(current) ? current : File.Exists(legacy) ? legacy : current;
    }

    public NoteImageReference Import(Guid noteId, string source)
    {
        using var stream = File.OpenRead(source);
        if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Images must be 25 MiB or smaller.");
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        string extension = decoder switch { PngBitmapDecoder => ".png", JpegBitmapDecoder => ".jpg",
            BmpBitmapDecoder => ".bmp", GifBitmapDecoder => ".gif",
            _ => throw new InvalidDataException("Use PNG, JPEG, BMP, or GIF (first frame only).") };
        ValidateSize(decoder.Frames[0]);
        _ = ImageDecodeService.Decode(source, 64); // Verify pixel data without retaining a full-resolution bitmap.
        string name = Guid.NewGuid().ToString("N") + extension;
        AtomicFile.Write(Path.Combine(DirectoryFor(noteId), name), output => { stream.Position = 0; stream.CopyTo(output); });
        return new(name, .3, Guid.NewGuid());
    }

    public NoteImageReference Import(Guid noteId, BitmapSource bitmap)
    {
        ValidateSize(bitmap);
        string name = Guid.NewGuid().ToString("N") + ".png";
        string path = Path.Combine(DirectoryFor(noteId), name);
        AtomicFile.Write(path, stream =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        });
        return new(name, .3, Guid.NewGuid());
    }

    public void CopyReferenced(Guid sourceId, Guid destinationId, IEnumerable<string> files)
    {
        foreach (string name in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string source = Resolve(sourceId, name);
            if (!File.Exists(source)) continue; // Preserve unresolved references, rather than inventing replacements.
            string destination = Path.Combine(DirectoryFor(destinationId), name);
            AtomicFile.Write(destination, output => { using var input = File.OpenRead(source); input.CopyTo(output); });
        }
    }

    public static void ValidateSize(BitmapSource bitmap)
    {
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0 || (long)bitmap.PixelWidth * bitmap.PixelHeight > MaximumPixels)
            throw new InvalidDataException("Images must contain no more than 40 megapixels.");
    }
}
