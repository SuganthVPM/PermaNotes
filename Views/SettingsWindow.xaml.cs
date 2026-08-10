using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DesktopNotes.Services;

namespace DesktopNotes.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly StartupService _startupService = new();
        private string _currentStoragePath = "";

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            StartWithWindowsCheckBox.IsChecked = _startupService.IsStartupEnabled();

            var settings = AppSettings.Load();
            
            _currentStoragePath = settings.CustomStoragePath;
            if (string.IsNullOrWhiteSpace(_currentStoragePath))
            {
                _currentStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes");
            }
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
            using var colorDialog = new System.Windows.Forms.ColorDialog();
            
            try 
            {
                var currentColorHex = (DefaultColorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "#FFF9C4";
                var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
            } 
            catch { }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                string hexColor = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                
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
            bool storagePathChanged = false;
            
            if (settings.CustomStoragePath != _currentStoragePath)
            {
                settings.CustomStoragePath = _currentStoragePath;
                storagePathChanged = true;
            }

            settings.ConfirmBeforeDelete = ConfirmDeleteCheckBox.IsChecked == true;
            settings.DefaultOpacity = OpacitySlider.Value;
            settings.DefaultNoteColor = (DefaultColorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "#FFF9C4";
            
            settings.Save();

            if (storagePathChanged)
            {
                // App.xaml.cs will handle the reload
                DialogResult = true; 
            }

            Close();
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

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopNotes", "settings.json");

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

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
