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
        public event EventHandler? RequestSearch;
        public event EventHandler? RequestNoteManager;
        public event EventHandler<NoteWindow>? RequestCloseNote;
        public event EventHandler<NoteWindow>? RequestDeleteNote;
        public event EventHandler<Note>? RequestDuplicateNote;
        public event EventHandler? RequestSettings;
        public event EventHandler? RequestExitApp;

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
            UpdateLockUI();

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
                
                UpdateTextContrast(textColor);

                LockNoteBtn.Foreground = iconColor;
                NewNoteBtn.Foreground = iconColor;
                SearchNoteBtn.Foreground = iconColor;
                DeleteNoteBtn.Foreground = iconColor;
                CloseNoteBtn.Foreground = iconColor;
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
                
                UpdateTextContrast((SolidColorBrush)ContentRichTextBox.Foreground);
                
                LockNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                NewNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                SearchNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                DeleteNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                CloseNoteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
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
            if (NoteModel.IsLocked) return;

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

            bool isHighlighted = IsSelectionHighlighted();

            if (isHighlighted)
            {
                // Toggle off
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, ContentRichTextBox.Foreground);
            }
            else
            {
                // Toggle on with solid yellow
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, System.Windows.Media.Brushes.Yellow);
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22)));
            }
            
            // Re-trigger text changed to save the new RTF data
            ContentRichTextBox_TextChanged(this, null!);
        }

        private void StrikethroughText_Click(object sender, RoutedEventArgs e)
        {
            if (ContentRichTextBox.Selection.IsEmpty) return;

            var currentDecorations = ContentRichTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            bool hasStrikethrough = false;

            if (currentDecorations != null)
            {
                foreach (var decoration in currentDecorations)
                {
                    if (decoration.Location == TextDecorationLocation.Strikethrough)
                    {
                        hasStrikethrough = true;
                        break;
                    }
                }
            }

            if (hasStrikethrough)
            {
                // To remove strikethrough without losing underline, we'd need to create a new collection
                // For simplicity, we can just clear it. But let's try to remove only strikethrough if we can
                var newCollection = new TextDecorationCollection();
                if (currentDecorations != null)
                {
                    foreach (var decoration in currentDecorations)
                    {
                        if (decoration.Location != TextDecorationLocation.Strikethrough)
                        {
                            newCollection.Add(decoration);
                        }
                    }
                }
                ContentRichTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, newCollection);
            }
            else
            {
                var newCollection = new TextDecorationCollection();
                if (currentDecorations != null && currentDecorations != DependencyProperty.UnsetValue)
                {
                    newCollection.Add(currentDecorations);
                }
                newCollection.Add(TextDecorations.Strikethrough);
                ContentRichTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, newCollection);
            }

            ContentRichTextBox_TextChanged(this, null!);
        }

        private void FontSizeMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && int.TryParse(mi.Tag?.ToString(), out int size))
            {
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, (double)size);
                ContentRichTextBox_TextChanged(this, null!);
            }
        }

        private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            if (ContentRichTextBox.Selection.IsEmpty) return;
            var currentSizeObj = ContentRichTextBox.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (currentSizeObj is double currentSize)
            {
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, currentSize + 2);
                ContentRichTextBox_TextChanged(this, null!);
            }
        }

        private void DecreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            if (ContentRichTextBox.Selection.IsEmpty) return;
            var currentSizeObj = ContentRichTextBox.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (currentSizeObj is double currentSize && currentSize > 4)
            {
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, currentSize - 2);
                ContentRichTextBox_TextChanged(this, null!);
            }
        }

        private void InsertTimestamp_Click(object sender, RoutedEventArgs e)
        {
            var timeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            ContentRichTextBox.CaretPosition.InsertTextInRun(timeString + " ");
            ContentRichTextBox.CaretPosition = ContentRichTextBox.CaretPosition.GetPositionAtOffset(timeString.Length + 1) ?? ContentRichTextBox.CaretPosition;
            ContentRichTextBox.Focus();
            ContentRichTextBox_TextChanged(this, null!);
        }

        private bool IsSelectionHighlighted()
        {
            // Walk through the text elements in the selection to check if any have a non-transparent background
            TextPointer? pos = ContentRichTextBox.Selection.Start;
            TextPointer? end = ContentRichTextBox.Selection.End;
            
            while (pos != null && pos.CompareTo(end) < 0)
            {
                if (pos.Parent is Inline inline)
                {
                    if (inline.Background is SolidColorBrush bg && bg.Color.A > 0 && bg.Color != System.Windows.Media.Colors.Transparent)
                    {
                        return true;
                    }
                }
                else if (pos.Parent is Paragraph para)
                {
                    if (para.Background is SolidColorBrush bg && bg.Color.A > 0 && bg.Color != System.Windows.Media.Colors.Transparent)
                    {
                        return true;
                    }
                }
                pos = pos.GetNextContextPosition(LogicalDirection.Forward);
            }
            return false;
        }

        private void UpdateTextContrast(SolidColorBrush defaultTextColor)
        {
            var darkText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
            
            // First apply default to the entire document
            var fullRange = new TextRange(ContentRichTextBox.Document.ContentStart, ContentRichTextBox.Document.ContentEnd);
            fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, defaultTextColor);

            // Then iterate to find yellow backgrounds and fix them
            TextPointer? pointer = ContentRichTextBox.Document.ContentStart;
            while (pointer != null)
            {
                if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart)
                {
                    if (pointer.GetAdjacentElement(LogicalDirection.Forward) is TextElement element)
                    {
                        var modernYellow = System.Windows.Media.Color.FromArgb(0x70, 0xFF, 0xEB, 0x3B);
                        if (element.Background is SolidColorBrush bg && (bg.Color == System.Windows.Media.Colors.Yellow || bg.Color == modernYellow))
                        {
                            element.Foreground = darkText;
                        }
                    }
                }
                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        private void ContentRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            // Trigger popup logic only if keyboard is used (Mouse released)
            if (Mouse.LeftButton == MouseButtonState.Released)
            {
                CheckFormatPopup();
            }
        }

        private void ContentRichTextBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            CheckFormatPopup();
        }

        private void CheckFormatPopup()
        {
            if (NoteModel.IsLocked)
            {
                FormatPopup.IsOpen = false;
                return;
            }

            if (!ContentRichTextBox.Selection.IsEmpty && !string.IsNullOrWhiteSpace(ContentRichTextBox.Selection.Text))
            {
                FormatPopup.IsOpen = true;
            }
            else
            {
                FormatPopup.IsOpen = false;
            }
        }

        private void ContentRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || NoteModel.IsLocked) return;

            // Generate RTF on the fly
            var range = new TextRange(ContentRichTextBox.Document.ContentStart, ContentRichTextBox.Document.ContentEnd);
            using var ms = new MemoryStream();
            range.Save(ms, System.Windows.DataFormats.Rtf);
            NoteModel.RtfText = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            NoteModel.Text = range.Text;
            
            NoteChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
                HighlightText_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                LockNoteButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.X && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                StrikethroughText_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                InsertTimestamp_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                NewNoteButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async void ShowSavedIndicator()
        {
            SavedIndicator.Opacity = 1;
            await System.Threading.Tasks.Task.Delay(1000);
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
            SavedIndicator.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // --- Note actions ---

        private void LockNoteButton_Click(object sender, RoutedEventArgs e)
        {
            NoteModel.IsLocked = !NoteModel.IsLocked;
            UpdateLockUI();
            NoteModel.UpdatedAt = DateTime.Now;
            NoteChanged?.Invoke(this, EventArgs.Empty);
            ShowSavedIndicator();
        }

        private void UpdateLockUI()
        {
            if (NoteModel.IsLocked)
            {
                // Show locked Padlock (Solid with keyhole)
                LockIconPath.Data = Geometry.Parse("M 19 9 L 18 9 L 18 6 C 18 2.7 15.3 0 12 0 C 8.7 0 6 2.7 6 6 L 6 9 L 5 9 C 3.9 9 3 9.9 3 11 L 3 20 C 3 21.1 3.9 22 5 22 L 19 22 C 20.1 22 21 21.1 21 20 L 21 11 C 21 9.9 20.1 9 19 9 Z M 14 9 L 14 6 C 14 4.9 13.1 4 12 4 C 10.9 4 10 4.9 10 6 L 10 9 L 14 9 Z M 12 17 A 1.5 1.5 0 0 0 12.75 14.33 L 12.75 12 A 0.75 0.75 0 0 0 11.25 12 L 11.25 14.33 A 1.5 1.5 0 0 0 12 17 Z");
                LockNoteBtn.ToolTip = "Unlock Note";
                
                ContentRichTextBox.IsReadOnly = true;
                ResizeMode = ResizeMode.NoResize;
                TitleBlock.Cursor = System.Windows.Input.Cursors.Arrow;
                FormatPopup.IsOpen = false;
            }
            else
            {
                // Show unlocked Padlock (Solid with keyhole, left hanger swung open, even length)
                LockIconPath.Data = Geometry.Parse("M 19 9 L 5 9 C 3.9 9 3 9.9 3 11 L 3 20 C 3 21.1 3.9 22 5 22 L 19 22 C 20.1 22 21 21.1 21 20 L 21 11 C 21 9.9 20.1 9 19 9 Z M 10 9 L 10 6 C 10 2.7 7.3 0 4 0 C 0.7 0 -2 2.7 -2 6 L -2 9 C -2 10.1 -0.9 11 0 11 C 0.9 11 2 10.1 2 9 L 2 6 C 2 4.9 2.9 4 4 4 C 5.1 4 6 4.9 6 6 L 6 9 Z M 12 17 A 1.5 1.5 0 0 0 12.75 14.33 L 12.75 12 A 0.75 0.75 0 0 0 11.25 12 L 11.25 14.33 A 1.5 1.5 0 0 0 12 17 Z");
                LockNoteBtn.ToolTip = "Lock Note";
                
                ContentRichTextBox.IsReadOnly = false;
                ResizeMode = ResizeMode.CanResize;
                TitleBlock.Cursor = System.Windows.Input.Cursors.IBeam;
            }
        }

        private void NewNoteButton_Click(object sender, RoutedEventArgs e)
        {
            RequestNewNote?.Invoke(this, EventArgs.Empty);
        }

        private void SearchNoteButton_Click(object sender, RoutedEventArgs e)
        {
            RequestSearch?.Invoke(this, EventArgs.Empty);
        }

        private void CloseNoteMenu_Click(object sender, RoutedEventArgs e)
        {
            RequestCloseNote?.Invoke(this, this);
        }

        private void ExportNoteAsText_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = NoteModel.Title
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, NoteModel.Text);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to export note:\n{ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CloseNoteButton_Click(object sender, RoutedEventArgs e)
        {
            RequestCloseNote?.Invoke(this, this);
        }

        private void ExitAppMenu_Click(object sender, RoutedEventArgs e) => RequestExitApp?.Invoke(this, EventArgs.Empty);
        
        private void NoteManagerMenu_Click(object sender, RoutedEventArgs e) => RequestNoteManager?.Invoke(this, EventArgs.Empty);

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
