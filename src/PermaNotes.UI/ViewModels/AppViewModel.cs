using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaNotes.Core.Models;
using PermaNotes.Core.Services;
using PermaNotes.Core.Storage;

namespace PermaNotes.UI.ViewModels
{
    public partial class AppViewModel : ObservableObject
    {
        private readonly NoteStorageService _storageService;
        private readonly IClickThroughService? _clickThroughService;
        private readonly IDesktopPinService? _desktopPinService;
        private readonly IStartupService? _startupService;

        public ObservableCollection<NoteViewModel> Notes { get; } = new();

        public event EventHandler<NoteViewModel>? NoteCreated;
        public event EventHandler<NoteViewModel>? NoteClosed;
        public event EventHandler<NoteViewModel>? NoteDeleted;
        public event EventHandler? RequestOpenManager;
        public event EventHandler? RequestOpenSettings;

        public AppViewModel(
            NoteStorageService? storageService = null,
            IClickThroughService? clickThroughService = null,
            IDesktopPinService? desktopPinService = null,
            IStartupService? startupService = null)
        {
            _storageService = storageService ?? new NoteStorageService();
            _clickThroughService = clickThroughService;
            _desktopPinService = desktopPinService;
            _startupService = startupService;

            LoadNotes();
        }

        public void LoadNotes()
        {
            Notes.Clear();
            var loaded = _storageService.LoadNotes();

            if (loaded.Count == 0)
            {
                // Create an initial welcome note on first launch
                var welcomeNote = new Note
                {
                    Title = "Welcome to PermaNotes",
                    Text = "Welcome to PermaNotes!\n\n• Double-click title to rename\n• Drag header to move\n• Drag bottom-right corner to resize\n• Right-click for colors and formatting",
                    RtfText = "",
                    X = 250,
                    Y = 200,
                    Width = 320,
                    Height = 280,
                    BackgroundColor = "#FFF9C4",
                    Opacity = 1.0,
                    IsAlwaysOnTop = false,
                    IsClosed = false
                };
                var vm = new NoteViewModel(welcomeNote, SaveDebounced);
                Notes.Add(vm);
                SaveImmediate();
            }
            else
            {
                foreach (var note in loaded)
                {
                    Notes.Add(new NoteViewModel(note, SaveDebounced));
                }
            }
        }

        public void SaveDebounced()
        {
            _storageService.SaveNotesDebounced(Notes.Select(n => n.Model).ToList(), delayMs: 500);
        }

        public void SaveImmediate()
        {
            _storageService.SaveNotesImmediate(Notes.Select(n => n.Model).ToList());
        }

        [RelayCommand]
        public void NewNote()
        {
            CreateNewNote();
        }

        public NoteViewModel CreateNewNote()
        {
            var settings = AppSettings.Load();
            var newNote = new Note
            {
                Title = "Untitled Note",
                Text = string.Empty,
                RtfText = string.Empty,
                X = 200 + (Notes.Count % 10) * 25,
                Y = 200 + (Notes.Count % 10) * 25,
                Width = 320,
                Height = 280,
                BackgroundColor = settings.DefaultNoteColor,
                Opacity = settings.DefaultOpacity,
                IsAlwaysOnTop = false,
                IsClosed = false
            };

            var vm = new NoteViewModel(newNote, SaveDebounced);
            Notes.Add(vm);
            SaveDebounced();
            NoteCreated?.Invoke(this, vm);
            return vm;
        }

        [RelayCommand]
        public void CloseNote(NoteViewModel vm)
        {
            vm.IsClosed = true;
            SaveDebounced();
            NoteClosed?.Invoke(this, vm);
        }

        [RelayCommand]
        public void OpenNote(NoteViewModel vm)
        {
            vm.IsClosed = false;
            SaveDebounced();
            NoteCreated?.Invoke(this, vm);
        }

        [RelayCommand]
        public void DeleteNote(NoteViewModel vm)
        {
            Notes.Remove(vm);
            SaveImmediate();
            NoteDeleted?.Invoke(this, vm);
        }

        [RelayCommand]
        public void DuplicateNote(NoteViewModel vm)
        {
            var clone = new Note
            {
                Title = vm.Title + " (Copy)",
                Text = vm.Text,
                RtfText = vm.RtfText,
                X = vm.X + 25,
                Y = vm.Y + 25,
                Width = vm.Width,
                Height = vm.Height,
                BackgroundColor = vm.BackgroundColor,
                Opacity = vm.Opacity,
                IsAlwaysOnTop = vm.IsAlwaysOnTop,
                IsClosed = false
            };

            var duplicateVm = new NoteViewModel(clone, SaveDebounced);
            Notes.Add(duplicateVm);
            SaveDebounced();
            NoteCreated?.Invoke(this, duplicateVm);
        }

        [RelayCommand]
        public void ShowAllNotes()
        {
            foreach (var note in Notes)
            {
                note.IsClosed = false;
                NoteCreated?.Invoke(this, note);
            }
            SaveDebounced();
        }

        [RelayCommand]
        public void HideAllNotes()
        {
            foreach (var note in Notes)
            {
                note.IsClosed = true;
                NoteClosed?.Invoke(this, note);
            }
            SaveDebounced();
        }

        [RelayCommand]
        public void OpenNoteManager()
        {
            RequestOpenManager?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void OpenSettings()
        {
            RequestOpenSettings?.Invoke(this, EventArgs.Empty);
        }
    }
}
