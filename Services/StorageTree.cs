using System.IO;

namespace DesktopNotes.Services;

internal static class StorageTree
{
    public static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<string>(); pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked storage folders are not supported for migration or cleanup.");
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked storage files are not supported for migration or cleanup.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                else yield return entry;
            }
        }
    }
}
