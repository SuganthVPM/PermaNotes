using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace DesktopNotes.Services;

/// <summary>Copy/verify/switch migration. Source is deliberately retained for rollback.</summary>
public static class StorageMigrationService
{
    public static void CopyVerified(string sourceRoot, string destinationRoot)
    {
        string source = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar);
        string destination = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase)) return;
        if (destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || source.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a storage folder outside the current storage folder.");
        Directory.CreateDirectory(destination);
        // A resumed migration may target an existing directory tree. Validate its
        // links before any writes so file publication stays inside that tree.
        foreach (var _ in StorageTree.Files(destination)) { }
        string journal = Path.Combine(destination, ".permanotes-migration.json");
        if (Directory.EnumerateFileSystemEntries(destination).Any() && !File.Exists(journal))
            throw new IOException("The destination must be empty. Existing notes will not be overwritten.");
        if (File.Exists(journal))
        {
            var previous = JsonSerializer.Deserialize<MigrationRecord>(File.ReadAllText(journal));
            if (previous == null || !string.Equals(previous.Source, source, StringComparison.OrdinalIgnoreCase) || previous.InUse)
                throw new IOException("This destination already belongs to a note store. Choose an empty folder.");
        }
        AtomicFile.WriteText(journal, JsonSerializer.Serialize(new MigrationRecord(source, false)));
        var copied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in StorageTree.Files(source))
        {
            string relative = Path.GetRelativePath(source, file);
            if (relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || relative.StartsWith(".permanotes-migration", StringComparison.Ordinal)) continue;
            copied.Add(relative);
            string target = Path.Combine(destination, relative);
            AtomicFile.Write(target, output => { using var input = File.OpenRead(file); input.CopyTo(output); });
            using var oldStream = File.OpenRead(file); using var newStream = File.OpenRead(target);
            if (!SHA256.HashData(oldStream).SequenceEqual(SHA256.HashData(newStream)))
                throw new IOException("Storage verification failed: " + relative);
        }
        string notes = Path.Combine(destination, "Notes");
        if (Directory.Exists(notes))
            foreach (string file in Directory.GetFiles(notes, "*.rtf"))
            {
                if (copied.Contains(Path.GetRelativePath(destination, file))) continue;
                string retired = Path.Combine(destination, "Trash", "MigrationOrphans", Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(file));
                Directory.CreateDirectory(Path.GetDirectoryName(retired)!); File.Move(file, retired);
            }
        AtomicFile.WriteText(journal, JsonSerializer.Serialize(new MigrationRecord(source, true)));
    }
    internal static void MarkDestinationInUse(string root)
    {
        string journal = Path.Combine(root, ".permanotes-migration.json");
        if (!File.Exists(journal)) return;
        var migration = JsonSerializer.Deserialize<MigrationRecord>(File.ReadAllText(journal));
        if (migration is { InUse: false })
            AtomicFile.WriteText(journal, JsonSerializer.Serialize(migration with { InUse = true }));
    }

    public sealed record MigrationRecord(string Source, bool Complete, bool InUse = false);
}
