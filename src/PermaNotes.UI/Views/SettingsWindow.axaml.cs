using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PermaNotes.UI.ViewModels;

namespace PermaNotes.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void ColorSwatch_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hexColor && DataContext is SettingsViewModel vm)
            {
                vm.DefaultNoteColor = hexColor;
            }
        }

        private async void BrowseStorage_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;

            var topLevel = GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = "Select PermaNotes Storage Directory",
                    AllowMultiple = false
                };

                var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (result.Count > 0)
                {
                    var selectedPath = result[0].Path.LocalPath;
                    vm.CustomStoragePath = selectedPath;
                }
            }
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.Save();
            }
            Close(true);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
