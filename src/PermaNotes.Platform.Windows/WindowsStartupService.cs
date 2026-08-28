using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.Windows
{
    /// <summary>
    /// Manages the Windows startup registry entry for PermaNotes under
    /// HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
    ///
    /// Using HKCU (current user) rather than HKLM means no admin elevation is needed.
    /// </summary>
    public class WindowsStartupService : IStartupService
    {
        private const string RegistryKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string AppName = "PermaNotes";

        /// <summary>Returns true if the HKCU Run entry exists for this app.</summary>
        [SupportedOSPlatform("windows")]
        public bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] IsStartupEnabled failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds or removes the HKCU Run entry.
        /// The entry points to the current process executable.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
                if (key == null) return;

                if (enabled)
                {
                    // Use the process executable path; surround with quotes to handle spaces
                    var exePath = Environment.ProcessPath
                                  ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] SetStartupEnabled({enabled}) failed: {ex.Message}");
            }
        }
    }
}
