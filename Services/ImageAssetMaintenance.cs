using System.IO;
using System.Text.Json;
using System.Windows.Documents;
using DesktopNotes.Models;

namespace DesktopNotes.Services;

/// <summary>Cold-start-only collection. Never run while editor history is alive.</summary>
public static class ImageAssetMaintenance
{
    public static int CollectAtColdStart(string root, DateTime utcNow)
    {
        string images = Path.Combine(root, "Images");
        if (!Directory.Exists(images)) return 0;
        if (!File.Exists(Path.Combine(root, "index.json")) && !File.Exists(Path.Combine(root, "index.json.bak")))
            throw new InvalidDataException("Metadata is unavailable; asset collection skipped until recovery completes.");
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string assetTrash = Path.Combine(root, "Trash", "Assets");
        var folders = Directory.EnumerateDirectories(images)
            .Concat(Directory.Exists(assetTrash) ? Directory.EnumerateDirectories(assetTrash) : Enumerable.Empty<string>())
            .Select(path => Path.GetFileName(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        void Keep(string shortId, string file)
        {
            foreach (string folder in folders.Where(f => f.StartsWith(shortId, StringComparison.OrdinalIgnoreCase)))
                references.Add(folder + "/" + file);
        }
        foreach (string index in new[] { Path.Combine(root, "index.json"), Path.Combine(root, "index.json.bak") })
        {
            if (!File.Exists(index)) continue;
            foreach (var note in JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(index)) ?? throw new InvalidDataException("Invalid note index."))
                foreach (string file in note.Images) Keep(NoteStorageService.GetShortId(note.Id), file);
        }
        // Complete the scan before touching assets. Unknown/corrupt content aborts collection.
        foreach (string file in StorageTree.Files(root))
        {
            if (!Path.GetFileName(file).Contains(".rtf", StringComparison.OrdinalIgnoreCase)) continue;
            string rtf = NoteStorageService.ReadRtf(file);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));
            var document = new FlowDocument(); var text = new TextRange(document.ContentStart, document.ContentEnd);
            text.Load(stream, System.Windows.DataFormats.Rtf);
            var owner = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file),
                @"_(?<id>[a-f0-9]{8})\.rtf(?:\.bak|\.[a-f0-9]{32})?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match marker in NoteDocumentCodec.ImageMarkers.Matches(text.Text))
            {
                if (!owner.Success || !NoteImageReference.TryParse(marker.Value, out var reference))
                    throw new InvalidDataException("Unknown image owner or reference. Asset collection skipped.");
                Keep(owner.Groups["id"].Value, reference.FileName);
            }
        }
        int collected = 0;
        foreach (string folder in Directory.GetDirectories(images))
        {
            string note = Path.GetFileName(folder);
            if (!System.Text.RegularExpressions.Regex.IsMatch(note, "^[a-fA-F0-9]{8,32}$")) continue;
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (references.Contains(note + "/" + name) || utcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromDays(7)) continue;
                string trash = Path.Combine(root, "Trash", "Assets", note, name);
                Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
                if (File.Exists(trash)) continue;
                File.Move(file, trash); File.SetLastWriteTimeUtc(trash, utcNow); collected++;
            }
        }
        if (Directory.Exists(assetTrash))
            foreach (string file in Directory.EnumerateFiles(assetTrash, "*", SearchOption.AllDirectories))
            {
                string key = Path.GetFileName(Path.GetDirectoryName(file)) + "/" + Path.GetFileName(file);
                if (!references.Contains(key) && utcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromDays(7)) File.Delete(file);
            }
        return collected;
    }
}
