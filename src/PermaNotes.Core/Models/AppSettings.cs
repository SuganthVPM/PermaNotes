using System;
using System.IO;
using System.Text.Json;

namespace PermaNotes.Core.Models
{
    public class AppSettings
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public double DefaultOpacity { get; set; } = 1.0;
        public string DefaultNoteColor { get; set; } = "#FFF9C4";
        public string CustomStoragePath { get; set; } = "";

#if DEBUG
        public static string PrimarySettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PermaNotes_Dev", "settings.json");
#else
        public static string PrimarySettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PermaNotes", "settings.json");
#endif

        public static string LegacySettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopNotes", "settings.json");

        public static AppSettings Load(string? primaryPath = null, string? legacyPath = null)
        {
            var primary = primaryPath ?? PrimarySettingsPath;
            var legacy = legacyPath ?? LegacySettingsPath;

            try
            {
                // 1. Primary path (%LOCALAPPDATA%\PermaNotes\settings.json)
                if (File.Exists(primary))
                {
                    var json = File.ReadAllText(primary);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }

                // 2. Legacy fallback (%LOCALAPPDATA%\DesktopNotes\settings.json)
                if (File.Exists(legacy))
                {
                    var json = File.ReadAllText(legacy);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                    // Migrate forward to primary path immediately
                    settings.Save(primary);
                    return settings;
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save(string? targetPath = null)
        {
            var path = targetPath ?? PrimarySettingsPath;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
