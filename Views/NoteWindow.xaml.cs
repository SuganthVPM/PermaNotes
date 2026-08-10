using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DesktopNotes.Models;

namespace DesktopNotes.Views
{
    public partial class NoteWindow : Window
    {
        public Note NoteModel { get; private set; }

        public event EventHandler? NoteChanged;
        public event EventHandler? AlwaysOnTopChanged;
        public event EventHandler? RequestNewNote;
        public event EventHandler<NoteWindow>? RequestDeleteNote;
        public event EventHandler<Note>? RequestDuplicateNote;
        public event EventHandler? RequestSettings;

        private bool _isInitializing = true;

        // Border colors matched to note background colors
        private static readonly Dictionary<string, string> BorderColorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "#FFF9C4", "#D0C070" }, // Yellow
            { "#FFE0B2", "#DCA457" }, // Orange
            { "#90CAF9", "#5B9BD5" }, // Blue
            { "#80CBC4", "#529B94" }, // Teal
            { "#A5D6A7", "#6DAF72" }, // Green
            { "#CE93D8", "#A163AB" }, // Purple
            { "#F48FB1", "#D46A8A" }, // Pink
            { "#FAFAFA", "#C0C0C0" }, // White
            { "#E0E0E0", "#A0A0A0" }, // Gray
        };

        public NoteWindow(Note note)
        {
            InitializeComponent();
            NoteModel = note;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Left = NoteModel.X;
            Top = NoteModel.Y;
            Width = NoteModel.Width;
            Height = NoteModel.Height;
            TitleBlock.Text = NoteModel.Title;
            Topmost = NoteModel.IsAlwaysOnTop;

            if (TryFindResource("NoteContextMenu") is ContextMenu cm)
            {
                foreach (var item in cm.Items)
                {
                    if (item is MenuItem mi && mi.Header?.ToString() == "Always on Top")
                    {
                        mi.IsChecked = NoteModel.IsAlwaysOnTop;
                        break;
                    }
                }
            }

            // Load RTF content or fallback to plain text
            if (!string.IsNullOrEmpty(NoteModel.RtfText))
            {
                try
                {
                    var range = new TextRange(ContentRichTextBox.Document.ContentStart, ContentRichTextBox.Document.ContentEnd);
                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(NoteModel.RtfText));
                    range.Load(ms, System.Windows.DataFormats.Rtf);
                }
                catch { }
            }
            else if (!string.IsNullOrEmpty(NoteModel.Text))
            {
                ContentRichTextBox.Document.Blocks.Clear();
                ContentRichTextBox.Document.Blocks.Add(new Paragraph(new Run(NoteModel.Text)));
            }

            ApplyBackgroundColor(NoteModel.BackgroundColor);

            _isInitializing = false;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                // Prevent maximizing to full screen so user doesn't lose resize grips
                WindowState = WindowState.Normal;
            }
        }

        // --- Background color with adaptive border ---

        public void ApplyBackgroundColor(string hexColor)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                color.A = (byte)(NoteModel.Opacity * 255);
                CardBorder.Background = new SolidColorBrush(color);
                NoteModel.BackgroundColor = hexColor;

                // Adaptive border color
                if (BorderColorMap.TryGetValue(hexColor, out var borderHex))
                {
                    var borderColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderHex);
                    borderColor.A = (byte)(NoteModel.Opacity * 255);
                    CardBorder.BorderBrush = new SolidColorBrush(borderColor);
                }
                else
                {
                    // Darken the note color by 30% for border
                    var borderColor = System.Windows.Media.Color.FromRgb(
                        (byte)(color.R * 0.7),
                        (byte)(color.G * 0.7),
                        (byte)(color.B * 0.7));
                    borderColor.A = (byte)(NoteModel.Opacity * 255);
                    CardBorder.BorderBrush = new SolidColorBrush(borderColor);
                }

                // Adaptive Text Color for contrast (especially for dark custom colors)
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                bool isDark = luminance < 0.5;
                
                var textColor = isDark ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
                var headerColor = isDark ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                var iconColor = isDark ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));

                TitleBlock.Foreground = headerColor;
                TitleEditBox.Foreground = headerColor;
                ContentRichTextBox.Foreground = textColor;
                ContentRichTextBox.CaretBrush = textColor; // Cursor color for the I-beam replacement
                NewNoteBtn.Foreground = iconColor;
                DeleteNoteBtn.Foreground = iconColor;
            }
            catch
            {
                var color = System.Windows.Media.Color.FromRgb(0xFF, 0xF9, 0xC4);
                color.A = (byte)(NoteModel.Opacity * 255);
                CardBorder.Background = new SolidColorBrush(color);
                
                var borderColor = System.Windows.Media.Color.FromRgb(0xD0, 0xC0, 0x70);
                borderColor.A = (byte)(NoteModel.Opacity * 255);
                CardBorder.BorderBrush = new SolidColorBrush(borderColor);

                TitleBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                TitleEditBox.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                ContentRichTextBox.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
                ContentRichTextBox.CaretBrush = ContentRichTextBox.Foreground;
                NewNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                DeleteNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
            }
        }

        // --- Title editing: TextBlock by default, double-click to edit ---

        private void TitleBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click: enter edit mode
                TitleEditBox.Text = NoteModel.Title;
                TitleBlock.Visibility = Visibility.Collapsed;
                TitleEditBox.Visibility = Visibility.Visible;
                TitleEditBox.Focus();
                TitleEditBox.SelectAll();
                e.Handled = true;
            }
            // Single click: allow drag (handled by Header_MouseLeftButtonDown)
        }

        private void CommitTitleEdit()
        {
            var newTitle = TitleEditBox.Text.Trim();
            if (string.IsNullOrEmpty(newTitle)) newTitle = "Untitled Note";

            TitleBlock.Text = newTitle;
            TitleBlock.Visibility = Visibility.Visible;
            TitleEditBox.Visibility = Visibility.Collapsed;

            if (NoteModel.Title != newTitle)
            {
                NoteModel.Title = newTitle;
                NoteModel.UpdatedAt = DateTime.Now;
                NoteChanged?.Invoke(this, EventArgs.Empty);
                ShowSavedIndicator();
            }
        }

        private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTitleEdit();
        }

        private void TitleEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                if (e.Key == Key.Escape)
                    TitleEditBox.Text = NoteModel.Title; // revert

                CommitTitleEdit();
                e.Handled = true;
            }
        }

        // --- Header drag ---

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                UpdatePositionAndSize();
            }
        }

        // --- Resize tracking ---

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (!_isInitializing)
            {
                UpdatePositionAndSize();
            }
        }

        private void UpdatePositionAndSize()
        {
            NoteModel.X = Left;
            NoteModel.Y = Top;
            NoteModel.Width = Width;
            NoteModel.Height = Height;
            NoteModel.UpdatedAt = DateTime.Now;
            NoteChanged?.Invoke(this, EventArgs.Empty);
        }

        // --- Content editing (RichText) ---

        private void HighlightText_Click(object sender, RoutedEventArgs e)
        {
            if (ContentRichTextBox.Selection.IsEmpty) return;

            var currentBackground = ContentRichTextBox.Selection.GetPropertyValue(TextElement.BackgroundProperty);
            if (currentBackground is SolidColorBrush brush && brush.Color == System.Windows.Media.Colors.Yellow)
            {
                // Toggle off
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, DependencyProperty.UnsetValue);
            }
            else
            {
                // Toggle on
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, System.Windows.Media.Brushes.Yellow);
            }
            
            // Re-trigger text changed to save the new RTF data
            ContentRichTextBox_TextChanged(this, null!);
        }

        private void ContentRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;

            var range = new TextRange(ContentRichTextBox.Document.ContentStart, ContentRichTextBox.Document.ContentEnd);
            using var ms = new MemoryStream();
            range.Save(ms, System.Windows.DataFormats.Rtf);
            NoteModel.RtfText = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            NoteModel.Text = range.Text.TrimEnd('\r', '\n');
            NoteModel.UpdatedAt = DateTime.Now;

            NoteChanged?.Invoke(this, EventArgs.Empty);
            ShowSavedIndicator();
        }

        private async void ShowSavedIndicator()
        {
            SavedIndicator.Opacity = 1;
            await System.Threading.Tasks.Task.Delay(1000);
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
            SavedIndicator.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // --- Note actions ---

        private void NewNoteButton_Click(object sender, RoutedEventArgs e)
        {
            RequestNewNote?.Invoke(this, EventArgs.Empty);
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = DesktopNotes.Views.AppSettings.Load();
            if (settings.ConfirmBeforeDelete)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Delete \"{NoteModel.Title}\"?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            RequestDeleteNote?.Invoke(this, this);
        }

        private void DuplicateNote_Click(object sender, RoutedEventArgs e)
        {
            var clone = new Note
            {
                Title = NoteModel.Title + " (copy)",
                Text = NoteModel.Text,
                RtfText = NoteModel.RtfText,
                X = Left + 30,
                Y = Top + 30,
                Width = Width,
                Height = Height,
                BackgroundColor = NoteModel.BackgroundColor,
                Opacity = NoteModel.Opacity,
                IsAlwaysOnTop = NoteModel.IsAlwaysOnTop
            };
            RequestDuplicateNote?.Invoke(this, clone);
        }

        // --- Color ---

        private void ColorMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string hexColor)
            {
                ApplyBackgroundColor(hexColor);
                NoteChanged?.Invoke(this, EventArgs.Empty);
                ShowSavedIndicator();
            }
        }

        private void CustomColorMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ColorWheelDialog(NoteModel.BackgroundColor)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.SelectedHexColor is string hexColor)
            {
                ApplyBackgroundColor(hexColor);
                NoteChanged?.Invoke(this, EventArgs.Empty);
                ShowSavedIndicator();
            }
        }

        // --- Opacity ---

        private void OpacityMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string opacityStr)
            {
                if (double.TryParse(opacityStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double opacity))
                {
                    NoteModel.Opacity = opacity;
                    ApplyBackgroundColor(NoteModel.BackgroundColor);
                    NoteModel.UpdatedAt = DateTime.Now;
                    NoteChanged?.Invoke(this, EventArgs.Empty);
                    ShowSavedIndicator();
                }
            }
        }

        // --- Always on Top ---

        private void AlwaysOnTopMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                NoteModel.IsAlwaysOnTop = menuItem.IsChecked;
                Topmost = NoteModel.IsAlwaysOnTop;
                NoteModel.UpdatedAt = DateTime.Now;
                AlwaysOnTopChanged?.Invoke(this, EventArgs.Empty);
                NoteChanged?.Invoke(this, EventArgs.Empty);
                ShowSavedIndicator();
            }
        }

        // --- Settings ---
        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            RequestSettings?.Invoke(this, EventArgs.Empty);
        }
    }
}
