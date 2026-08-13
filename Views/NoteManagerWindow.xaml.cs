using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopNotes.Models;

namespace DesktopNotes.Views
{
    public partial class NoteManagerWindow : Window
    {
        private readonly App _app;
        private readonly List<Note> _allNotes;
        private readonly ObservableCollection<NoteViewModel> _displayedNotes;

        public NoteManagerWindow(App app, List<Note> notes)
        {
            InitializeComponent();
            _app = app;
            _allNotes = notes;
            _displayedNotes = new ObservableCollection<NoteViewModel>();
            NotesListView.ItemsSource = _displayedNotes;
            
            RefreshList();
        }

        public void RefreshList()
        {
            var filter = SearchBox.Text.Trim().ToLowerInvariant();
            _displayedNotes.Clear();

            foreach (var note in _allNotes.OrderByDescending(n => n.UpdatedAt))
            {
                if (string.IsNullOrEmpty(filter) || note.Title.ToLowerInvariant().Contains(filter))
                {
                    _displayedNotes.Add(new NoteViewModel(note));
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            RefreshList();
        }

        private void NotesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (NotesListView.SelectedItem is NoteViewModel vm)
            {
                _app.ActivateNote(vm.Model);
                RefreshList();
            }
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            _app.Dispatcher.Invoke(() => {
                _app.SpawnNewNote();
            });
            RefreshList();
        }

        private void ToggleState_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListView.SelectedItem is NoteViewModel vm)
            {
                if (vm.Model.IsClosed)
                {
                    _app.ActivateNote(vm.Model);
                }
                else
                {
                    var window = System.Windows.Application.Current.Windows.OfType<NoteWindow>().FirstOrDefault(w => w.NoteModel == vm.Model);
                    if (window != null)
                    {
                        _app.CloseNoteWindow(window);
                    }
                    else
                    {
                        vm.Model.IsClosed = true;
                    }
                }
                RefreshList();
            }
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListView.SelectedItem is NoteViewModel vm)
            {
                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete '{vm.Title}'? This cannot be undone.", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    var window = System.Windows.Application.Current.Windows.OfType<NoteWindow>().FirstOrDefault(w => w.NoteModel == vm.Model);
                    if (window != null)
                    {
                        _app.DeleteNoteWindow(window);
                    }
                    else
                    {
                        _allNotes.Remove(vm.Model);
                        _app.OnNotesStateChanged();
                    }
                    RefreshList();
                }
            }
        }
    }

    public class NoteViewModel
    {
        public Note Model { get; }

        public NoteViewModel(Note model)
        {
            Model = model;
        }

        public string Title => Model.Title;
        public string BackgroundColor => Model.BackgroundColor;
        public DateTime UpdatedAt => Model.UpdatedAt;
        public string StatusText => Model.IsClosed ? "Closed" : "Open";
        public string StatusColor => Model.IsClosed ? "#888888" : "#4CAF50";
    }
}
