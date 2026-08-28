using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PermaNotes.UI.ViewModels
{
    public partial class NoteManagerViewModel : ObservableObject
    {
        private readonly AppViewModel _appViewModel;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<NoteViewModel> DisplayedNotes { get; } = new();

        public int TotalCount => _appViewModel.Notes.Count;
        public int OpenCount => _appViewModel.Notes.Count(n => !n.IsClosed);
        public int ClosedCount => _appViewModel.Notes.Count(n => n.IsClosed);

        public NoteManagerViewModel(AppViewModel appViewModel)
        {
            _appViewModel = appViewModel;
            _appViewModel.Notes.CollectionChanged += (_, _) => RefreshList();
            RefreshList();
        }

        partial void OnSearchTextChanged(string value)
        {
            RefreshList();
        }

        public void RefreshList()
        {
            var filter = SearchText.Trim().ToLowerInvariant();
            DisplayedNotes.Clear();

            var query = _appViewModel.Notes.AsEnumerable();

            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(n =>
                    (n.Title?.ToLowerInvariant().Contains(filter) ?? false) ||
                    (n.Text?.ToLowerInvariant().Contains(filter) ?? false));
            }

            foreach (var note in query.OrderByDescending(n => n.UpdatedAt))
            {
                DisplayedNotes.Add(note);
            }

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(OpenCount));
            OnPropertyChanged(nameof(ClosedCount));
        }

        [RelayCommand]
        public void ToggleNoteState(NoteViewModel note)
        {
            if (note.IsClosed)
            {
                _appViewModel.OpenNote(note);
            }
            else
            {
                _appViewModel.CloseNote(note);
            }
            RefreshList();
        }

        [RelayCommand]
        public void DeleteNote(NoteViewModel note)
        {
            _appViewModel.DeleteNote(note);
            RefreshList();
        }

        [RelayCommand]
        public void NewNote()
        {
            _appViewModel.NewNote();
            RefreshList();
        }
    }
}
