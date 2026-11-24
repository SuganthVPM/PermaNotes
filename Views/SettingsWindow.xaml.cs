using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopNotes.Services;

namespace DesktopNotes.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly StartupService _startupService = new();
        private string _currentStoragePath = "";
        public string RequestedStoragePath => _currentStoragePath;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void LoadSettings()
        {
            StartWithWindowsCheckBox.IsChecked = _startupService.IsStartupEnabled();

            var settings = AppSettings.Load();
            
            _currentStoragePath = NoteStorageService.GetEffectiveStoragePath();
            StoragePathText.Text = _currentStoragePath;

            ConfirmDeleteCheckBox.IsChecked = settings.ConfirmBeforeDelete;
            OpacitySlider.Value = settings.DefaultOpacity;

            // Select matching default color
            bool colorFound = false;
            foreach (ComboBoxItem item in DefaultColorCombo.Items)
            {
                if (item.Tag is string tag && tag.Equals(settings.DefaultNoteColor, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsSelected = true;
                    colorFound = true;
                    break;
                }
            }
            if (!colorFound)
            {
                var customItem = new ComboBoxItem { Content = "Custom", Tag = settings.DefaultNoteColor, IsSelected = true };
                DefaultColorCombo.Items.Add(customItem);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"{(int)(OpacitySlider.Value * 100)}%";
            }
        }

        private void BrowseStorage_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder to store your notes",
                SelectedPath = _currentStoragePath
            };

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _currentStoragePath = fbd.SelectedPath;
                StoragePathText.Text = _currentStoragePath;
            }
        }

        private void CustomColor_Click(object sender, RoutedEventArgs e)
        {
            var currentColorHex = (DefaultColorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "#FFF9C4";
            var dialog = new ColorWheelDialog(currentColorHex)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.SelectedHexColor is string hexColor)
            {
                ComboBoxItem? customItem = null;
                foreach (ComboBoxItem item in DefaultColorCombo.Items)
                {
                    if (item.Content?.ToString() == "Custom")
                    {
                        customItem = item;
                        break;
                    }
                }
                
                if (customItem == null)
                {
                    customItem = new ComboBoxItem { Content = "Custom" };
                    DefaultColorCombo.Items.Add(customItem);
                }
                
                customItem.Tag = hexColor;
                customItem.IsSelected = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Start with Windows
            _startupService.SetStartupEnabled(StartWithWindowsCheckBox.IsChecked == true);

            // Save app settings
            var settings = AppSettings.Load();
            bool storagePathChanged = !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(_currentStoragePath)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(NoteStorageService.GetEffectiveStoragePath())),
                StringComparison.OrdinalIgnoreCase);

            settings.ConfirmBeforeDelete = ConfirmDeleteCheckBox.IsChecked == true;
            settings.DefaultOpacity = OpacitySlider.Value;
            settings.DefaultNoteColor = (DefaultColorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "#FFF9C4";
            
            if (!settings.Save())
            {
                System.Windows.MessageBox.Show(this, "PermaNotes could not save your settings.", "Settings");
                return;
            }

            DialogResult = storagePathChanged;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Simple settings model persisted to %LOCALAPPDATA%\DesktopNotes\settings.json
    /// </summary>
    public class AppSettings
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public double DefaultOpacity { get; set; } = 1.0;
        public string DefaultNoteColor { get; set; } = "#FFF9C4";
        public string CustomStoragePath { get; set; } = "";
        public string LastSeenVersion { get; set; } = "";

        /// <summary>
        /// When <c>true</c> raw URLs pasted or typed into notes are automatically
        /// converted to clickable hyperlinks. When <c>false</c> URLs stay as plain text.
        /// </summary>
        public bool HyperlinksEnabled { get; set; } = true;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if DEV
            "DesktopNotes_Dev", "settings.json");
#else
            "DesktopNotes", "settings.json");
#endif

        internal static bool HasSavedSettings => File.Exists(SettingsPath);

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public bool Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                DesktopNotes.Services.AtomicFile.WriteText(SettingsPath, json);
                return true;
            }
            catch { return false; }
        }
    }
}
