using System;
using System.IO;
using System.Linq;
using AvRichTextBox;

namespace PermaNotes.UI.Services
{
    public static class RtfMigration
    {
        public static void Migrate(string notesDir)
        {
            if (!Directory.Exists(notesDir)) return;

            var rtfFiles = Directory.GetFiles(notesDir, "*.rtf");
            foreach (var file in rtfFiles)
            {
                // Skip files that have already been migrated
                if (file.EndsWith(".backup.rtf", StringComparison.OrdinalIgnoreCase)) continue;

                var backupPath = file + ".backup";
                if (File.Exists(backupPath)) continue; // Already migrated

                try
                {
                    // Create a headless FlowDocument just to load and re-save the RTF
                    var flowDoc = new FlowDocument();
                    flowDoc.LoadRtfFromFile(file);
                    
                    // Backup original
                    File.Copy(file, backupPath, true);
                    
                    // Save as clean AvRichTextBox RTF format
                    flowDoc.SaveRtfToFile(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to migrate RTF file {file}: {ex.Message}");
                }
            }
        }
    }
}
