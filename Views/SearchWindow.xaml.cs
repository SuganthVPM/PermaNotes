using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopNotes.Models;

namespace DesktopNotes.Views
{
    public partial class SearchWindow : Window
    {
        private readonly IEnumerable<NoteWindow> _allNoteWindows;

        public SearchWindow(IEnumerable<NoteWindow> allNoteWindows)
        {
            InitializeComponent();
            _allNoteWindows = allNoteWindows;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            FilterNotes("");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            FilterNotes(SearchBox.Text.Trim());
        }

        private void FilterNotes(string query)
        {
            var results = new List<SearchResultItem>();
            foreach (var win in _allNoteWindows)
            {
                var note = win.NoteModel;
                string title = note.Title ?? "";
                string text = note.Text ?? "";
                
                if (string.IsNullOrEmpty(query) || 
                    title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResultItem
                    {
                        NoteWindow = win,
                        Title = title,
                        Snippet = text.Replace("\r", " ").Replace("\n", " ")
                    });
                }
            }
            ResultsList.ItemsSource = results;
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResultItem item)
            {
                // Bring the note to front
                if (!item.NoteWindow.IsVisible) item.NoteWindow.Show();
                if (item.NoteWindow.WindowState == WindowState.Minimized) item.NoteWindow.WindowState = WindowState.Normal;
                item.NoteWindow.Activate();
                item.NoteWindow.Topmost = true;
                item.NoteWindow.Topmost = item.NoteWindow.NoteModel.IsAlwaysOnTop; // revert to its original topmost state
                
                // Add temporary highlight effect if needed (e.g. animate opacity)
                
                Close();
            }
        }

        public class SearchResultItem
        {
            public NoteWindow NoteWindow { get; set; } = null!;
            public string Title { get; set; } = "";
            public string Snippet { get; set; } = "";
        }
    }
}
