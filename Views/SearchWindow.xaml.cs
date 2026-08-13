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
        private readonly IEnumerable<Note> _allNotes;

        public SearchWindow(IEnumerable<Note> allNotes)
        {
            InitializeComponent();
            _allNotes = allNotes;
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
            foreach (var note in _allNotes)
            {
                string title = note.Title ?? "";
                string text = note.Text ?? "";
                
                if (string.IsNullOrEmpty(query) || 
                    title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResultItem
                    {
                        NoteModel = note,
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
                // Delegate activation to the main app instance which can handle closed vs open states
                if (System.Windows.Application.Current is App app)
                {
                    app.ActivateNote(item.NoteModel);
                }
                
                Close();
            }
        }

        public class SearchResultItem
        {
            public Note NoteModel { get; set; } = null!;
            public string Title { get; set; } = "";
            public string Snippet { get; set; } = "";
        }
    }
}
