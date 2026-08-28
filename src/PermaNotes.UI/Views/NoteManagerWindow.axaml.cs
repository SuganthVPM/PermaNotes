using System;
using Avalonia.Controls;
using Avalonia.Input;
using PermaNotes.UI.ViewModels;

namespace PermaNotes.UI.Views
{
    public partial class NoteManagerWindow : Window
    {
        public event EventHandler<NoteViewModel>? NoteActivated;

        public NoteManagerWindow()
        {
            InitializeComponent();
        }

        public NoteManagerWindow(NoteManagerViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void NotesList_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (NotesListBox.SelectedItem is NoteViewModel selectedNote)
            {
                if (selectedNote.IsClosed)
                {
                    selectedNote.IsClosed = false;
                }
                NoteActivated?.Invoke(this, selectedNote);
            }
        }
    }
}
