using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;

namespace DesktopNotes.Services;

public static class ImageExportService
{
    public static void SaveOriginal(string source, string destination)
    {
        static string Extension(string file) => Path.GetExtension(file).ToLowerInvariant().Replace(".jpeg", ".jpg");
        if (Extension(source) != Extension(destination)) throw new InvalidDataException("Keep the original file extension, or use PNG/JPEG export to convert.");
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
        AtomicFile.Write(destination, output => { using var input = File.OpenRead(source); input.CopyTo(output); });
    }
    public static void Export(string source, string destination) => Export(ImageDecodeService.Decode(source), destination);

    public static void Export(BitmapSource bitmap, string destination)
    {
        string extension = Path.GetExtension(destination).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            throw new InvalidDataException("Choose a PNG or JPEG filename.");
        if (extension is ".jpg" or ".jpeg")
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                var rect = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
                drawing.DrawRectangle(Brushes.White, null, rect);
                drawing.DrawImage(bitmap, rect);
            }
            var flattened = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            flattened.Render(visual); flattened.Freeze(); bitmap = flattened;
        }
        AtomicFile.Write(destination, output =>
        {
            BitmapEncoder encoder = extension == ".png" ? new PngBitmapEncoder() : new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(output);
        });
    }
}
