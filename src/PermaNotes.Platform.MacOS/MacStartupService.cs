using System;
using System.IO;
using System.Text;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.MacOS
{
    /// <summary>
    /// Manages macOS startup via LaunchAgent property list (.plist)
    /// under ~/Library/LaunchAgents/com.permanotes.startup.plist.
    /// This is the standard, secure, and sandbox-compatible mechanism for macOS login items.
    /// </summary>
    public class MacStartupService : IStartupService
    {
        private const string PlistFileName = "com.permanotes.startup.plist";
        private const string Label = "com.permanotes.startup";

        private static string GetPlistPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "LaunchAgents", PlistFileName);
        }

        public bool IsStartupEnabled()
        {
            try
            {
                string path = GetPlistPath();
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacStartupService] IsStartupEnabled failed: {ex.Message}");
                return false;
            }
        }

        public void SetStartupEnabled(bool enabled)
        {
            try
            {
                string path = GetPlistPath();

                if (enabled)
                {
                    string? exePath = Environment.ProcessPath
                                      ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                    if (string.IsNullOrWhiteSpace(exePath)) return;

                    string dir = Path.GetDirectoryName(path)!;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string plistXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>{Label}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

                    File.WriteAllText(path, plistXml, new UTF8Encoding(false));
                }
                else
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacStartupService] SetStartupEnabled({enabled}) failed: {ex.Message}");
            }
        }
    }
}
