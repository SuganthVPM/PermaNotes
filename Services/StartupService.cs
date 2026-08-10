using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DesktopNotes.Services
{
    /// <summary>
    /// Manages "Start with Windows" functionality via HKCU registry Run key.
    /// No admin privileges required.
    /// </summary>
    public class StartupService
    {
        private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DesktopNotes";

        /// <summary>
        /// Returns the path to the currently running executable.
        /// </summary>
        private static string ExePath => Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DesktopNotes.exe");

        public bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        public void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key == null) return;

                if (enabled)
                {
                    key.SetValue(AppName, $"\"{ExePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup registry: {ex.Message}");
            }
        }
    }
}
