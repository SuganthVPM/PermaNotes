using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaNotes.Core.Models;
using PermaNotes.Core.Services;

namespace PermaNotes.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IStartupService? _startupService;
        private readonly Action? _onSettingsSaved;

        [ObservableProperty]
        private string _customStoragePath = string.Empty;

        [ObservableProperty]
        private bool _confirmBeforeDelete = true;

        [ObservableProperty]
        private double _defaultOpacity = 1.0;

        [ObservableProperty]
        private string _defaultNoteColor = "#FFF9C4";

        [ObservableProperty]
        private bool _startWithWindows;

        public SettingsViewModel(IStartupService? startupService = null, Action? onSettingsSaved = null)
        {
            _startupService = startupService;
            _onSettingsSaved = onSettingsSaved;

            LoadCurrentSettings();
        }

        public void LoadCurrentSettings()
        {
            var settings = AppSettings.Load();
            var path = settings.CustomStoragePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes");
            }
            CustomStoragePath = path;
            ConfirmBeforeDelete = settings.ConfirmBeforeDelete;
            DefaultOpacity = settings.DefaultOpacity;
            DefaultNoteColor = settings.DefaultNoteColor;

            StartWithWindows = _startupService?.IsStartupEnabled() ?? false;
        }

        [RelayCommand]
        public void Save()
        {
            var settings = AppSettings.Load();
            settings.CustomStoragePath = CustomStoragePath;
            settings.ConfirmBeforeDelete = ConfirmBeforeDelete;
            settings.DefaultOpacity = DefaultOpacity;
            settings.DefaultNoteColor = DefaultNoteColor;
            settings.Save();

            _startupService?.SetStartupEnabled(StartWithWindows);
            _onSettingsSaved?.Invoke();
        }
    }
}
