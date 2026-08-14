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
    /// <summary>
    /// The Note Manager window provides a central dashboard for all notes — both open and closed.
    ///
    /// Features:
    ///  - Lists every note, sorted by most-recently-updated first.
    ///  - Live search: filters by title as the user types.
    ///  - Double-click a row to activate (open/bring-to-front) a note.
    ///  - Toggle Open/Close: shows or hides a note without deleting it.
    ///  - Delete: permanently removes a note after a confirmation prompt.
    ///  - New Note: spawns a blank note and refreshes the list.
    ///  - Colour dot: shows the note's current background colour at a glance.
    ///  - Status badge: green "Open" or grey "Closed".
    ///
    /// Accessible via: System Tray → Note Manager, or right-click context menu → Note Manager.
    /// </summary>
    public partial class NoteManagerWindow : Window
    {
        /// <summary>Reference to the running <see cref="App"/> instance used to call window-lifecycle methods.</summary>
        private readonly App _app;

        /// <summary>The master list of all notes (open and closed), shared with App.xaml.cs.</summary>
        private readonly List<Note> _allNotes;

        /// <summary>The filtered, observable collection that the ListView is bound to.</summary>
        private readonly ObservableCollection<NoteViewModel> _displayedNotes;

        /// <summary>
        /// Initializes the Note Manager, binds the list view, and performs the initial refresh.
        /// </summary>
        /// <param name="app">The running application instance.</param>
        /// <param name="notes">The master list of all notes — mutated directly by delete operations.</param>
        public NoteManagerWindow(App app, List<Note> notes)
        {
            InitializeComponent();
            _app = app;
            _allNotes = notes;
            _displayedNotes = new ObservableCollection<NoteViewModel>();
            NotesListView.ItemsSource = _displayedNotes;
            
            RefreshList();
        }

        /// <summary>
        /// Repopulates <see cref="_displayedNotes"/> from <see cref="_allNotes"/>, applying
        /// the current search-box filter and sorting by most-recently-updated first.
        /// Call this after any note state change (create, delete, open, close).
        /// </summary>
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

        /// <summary>
        /// Handles live search: hides the watermark and re-filters the list on every keystroke.
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            RefreshList();
        }

        /// <summary>
        /// Double-clicking a row activates the corresponding note window (opens it if closed,
        /// or brings it to the front if already open).
        /// </summary>
        private void NotesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (NotesListView.SelectedItem is NoteViewModel vm)
            {
                _app.ActivateNote(vm.Model);
                RefreshList();
            }
        }

        /// <summary>Spawns a new blank note and refreshes the list.</summary>
        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            _app.Dispatcher.Invoke(() => {
                _app.SpawnNewNote();
            });
            RefreshList();
        }

        /// <summary>
        /// Toggles the open/closed state of the selected note.
        /// - If closed: calls <see cref="App.ActivateNote"/> to reopen it.
        /// - If open: closes the note window via <see cref="App.CloseNoteWindow"/>.
        /// </summary>
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

        /// <summary>
        /// Permanently deletes the selected note after user confirmation.
        /// If the note has an open window, delegates to <see cref="App.DeleteNoteWindow"/>.
        /// If the note is already closed (no window), removes it from the master list directly
        /// and triggers a debounced save via <see cref="App.OnNotesStateChanged"/>.
        /// </summary>
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

        /// <summary>
        /// Fallback toggle for notes that are partially off-screen and whose on-canvas
        /// handle is unreachable. Syncs the live <see cref="NoteWindow"/> if it exists.
        /// </summary>
        private void ToggleClickThrough_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListView.SelectedItem is NoteViewModel vm)
            {
                vm.Model.IsClickThrough = !vm.Model.IsClickThrough;

                // Sync the live window immediately (updates handle visibility + Topmost)
                var window = System.Windows.Application.Current.Windows.OfType<NoteWindow>()
                    .FirstOrDefault(w => w.NoteModel == vm.Model);
                window?.ApplyClickThroughState();

                _app.OnNotesStateChanged();
                RefreshList();
            }
        }
    }

    /// <summary>
    /// Lightweight presentation wrapper around <see cref="Note"/> for the Note Manager ListView.
    /// Exposes flat, bindable properties (title, colour, status text/colour, timestamp) without
    /// modifying the underlying model.
    /// </summary>
    public class NoteViewModel
    {
        /// <summary>The underlying note data model.</summary>
        public Note Model { get; }

        public NoteViewModel(Note model)
        {
            Model = model;
        }

        /// <summary>Display title of the note.</summary>
        public string Title => Model.Title;

        /// <summary>Hex background colour string, used to fill the colour dot in the list.</summary>
        public string BackgroundColor => Model.BackgroundColor;

        /// <summary>Timestamp of the last edit, shown in the "Last Updated" column.</summary>
        public DateTime UpdatedAt => Model.UpdatedAt;

        /// <summary>"Open", "Closed", or "Click-Through" status text.</summary>
        public string StatusText => Model.IsClosed ? "Closed" : (Model.IsClickThrough ? "Click-Through" : "Open");

        /// <summary>Green for open, grey for closed, orange for click-through.</summary>
        public string StatusColor => Model.IsClosed ? "#888888" : (Model.IsClickThrough ? "#FF9800" : "#4CAF50");
    }
}
