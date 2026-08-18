using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopNotes.Models;

namespace DesktopNotes.Views
{
    /// <summary>
    /// The main sticky-note window. Each instance corresponds to one <see cref="Note"/> model.
    ///
    /// Responsibilities:
    ///  - Rendering a note card (title, rich text content, header buttons).
    ///  - Drag-to-move and resize-tracking (persists X/Y/Width/Height to the model).
    ///  - Rich text formatting: Bold, Italic, Underline, Strikethrough, Highlight (toggle),
    ///    Font Size (absolute via menu + relative ±2 via toolbar), Insert Timestamp.
    ///  - Context menu interactions: forwarded to App.xaml.cs via events.
    ///  - Keyboard shortcuts: Ctrl+H (highlight), Ctrl+Shift+L (lock), Ctrl+Shift+X (strikethrough),
    ///    Ctrl+T (timestamp), Ctrl+N (new note).
    ///  - Floating format popup: appears on text selection, dismissed on collapse.
    ///  - Lock mode: disables all editing when <see cref="Note.IsLocked"/> is true.
    ///  - Adaptive contrast: text/icon colors flip to white on dark note backgrounds.
    ///
    /// All cross-window actions (new note, delete, close, settings, etc.) are surfaced as
    /// events and handled centrally in <see cref="App"/>.
    /// </summary>
    public partial class NoteWindow : Window
    {
        /// <summary>The data model this window represents.</summary>
        public Note NoteModel { get; private set; }

        // --- Events raised to App.xaml.cs for cross-window coordination ---

        /// <summary>Raised whenever note content, position, or metadata changes. Triggers debounced save.</summary>
        public event EventHandler? NoteChanged;

        /// <summary>Raised when the Always-on-Top toggle changes, so App can detach/attach the desktop owner.</summary>
        public event EventHandler? AlwaysOnTopChanged;

        /// <summary>Raised when the user requests a new blank note (toolbar button or Ctrl+N).</summary>
        public event EventHandler? RequestNewNote;

        /// <summary>Raised when the user opens the Search Notes panel.</summary>
        public event EventHandler? RequestSearch;

        /// <summary>Raised when the user opens the Note Manager window (context menu → Note Manager).</summary>
        public event EventHandler? RequestNoteManager;

        /// <summary>Raised when the user closes this note (hides it, marks IsClosed=true).</summary>
        public event EventHandler<NoteWindow>? RequestCloseNote;

        /// <summary>Raised when the user permanently deletes this note.</summary>
        public event EventHandler<NoteWindow>? RequestDeleteNote;

        /// <summary>Raised when the user duplicates this note. Carries the pre-cloned <see cref="Note"/> object.</summary>
        public event EventHandler<Note>? RequestDuplicateNote;

        /// <summary>Raised when the user opens the Settings dialog.</summary>
        public event EventHandler? RequestSettings;

        /// <summary>Raised when the user chooses Exit from the context menu.</summary>
        public event EventHandler? RequestExitApp;

        /// <summary>
        /// Suppresses change-tracking callbacks during initial data load so that
        /// restoring RTF content doesn't immediately trigger a save.
        /// </summary>
        private bool _isInitializing = true;

        /// <summary>
        /// Maps each preset note background hex colour to a darker border colour
        /// that complements it. Colours not in this map get a generic 30%-darkened border.
        /// </summary>
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

        /// <summary>
        /// Initializes a new <see cref="NoteWindow"/> for the given <paramref name="note"/>.
        /// UI data-binding and color/lock state are applied in <see cref="Window_Loaded"/>.
        /// </summary>
        public NoteWindow(Note note)
        {
            InitializeComponent();
            NoteModel = note;
        }

        // ---- Win32 Click-Through ----

        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = -20;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private PinWindow? _pinWindow;
        internal void SetDesktopZOrder(IntPtr hwndInsertAfter)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    DesktopNotes.Interop.NativeMethods.SetWindowPos(
                        hwnd, hwndInsertAfter,
                        0, 0, 0, 0,
                        0x0213 // SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER
                    );
                }
            }
            catch { }
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Left = NoteModel.X;
            Top = NoteModel.Y;
            Width = NoteModel.Width;
            Height = NoteModel.Height;
            TitleBlock.Text = NoteModel.Title;
            Topmost = NoteModel.IsAlwaysOnTop;

            LocationChanged += NoteWindow_LocationChanged;
            SizeChanged += NoteWindow_SizeChanged;

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
            ApplyClickThroughState(); // restore persisted click-through state

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

        /// <summary>
        /// Toggles highlight on the current text selection.
        /// Uses <see cref="IsSelectionHighlighted"/> to walk individual <see cref="Inline"/> runs,
        /// which avoids the unreliable <c>GetPropertyValue</c> behaviour on mixed-formatting selections.
        /// - If any run in the selection has a non-transparent background → removes highlight.
        /// - Otherwise → applies solid yellow with forced dark foreground for legibility.
        /// Shortcut: Ctrl+H. Also available in the Format popup toolbar and context menu.
        /// </summary>
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

        /// <summary>
        /// Toggles strikethrough on the current text selection.
        /// Preserves any existing text decorations (e.g. underline) by rebuilding the
        /// <see cref="TextDecorationCollection"/> rather than replacing it wholesale.
        /// Shortcut: Ctrl+Shift+X. Also available in the Format popup and context menu.
        /// </summary>
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

        /// <summary>
        /// Sets an absolute font size on the current selection via the Font Size context sub-menu.
        /// The target size is stored in the <c>Tag</c> property of the <see cref="MenuItem"/>.
        /// </summary>
        private void FontSizeMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && int.TryParse(mi.Tag?.ToString(), out int size))
            {
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, (double)size);
                ContentRichTextBox_TextChanged(this, null!);
            }
        }

        /// <summary>
        /// Increases the font size of selected text by 2pt.
        /// Reads the current size from <see cref="TextElement.FontSizeProperty"/>.
        /// No-ops when nothing is selected.
        /// </summary>
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

        /// <summary>
        /// Decreases the font size of selected text by 2pt, with a minimum of 4pt.
        /// No-ops when nothing is selected.
        /// </summary>
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

        /// <summary>
        /// Inserts the current date and time (format: <c>yyyy-MM-dd HH:mm</c>) at the caret position.
        /// Shortcut: Ctrl+T. Also available in the Insert context sub-menu and format popup toolbar.
        /// </summary>
        private void InsertTimestamp_Click(object sender, RoutedEventArgs e)
        {
            var timeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            ContentRichTextBox.CaretPosition.InsertTextInRun(timeString + " ");
            ContentRichTextBox.CaretPosition = ContentRichTextBox.CaretPosition.GetPositionAtOffset(timeString.Length + 1) ?? ContentRichTextBox.CaretPosition;
            ContentRichTextBox.Focus();
            ContentRichTextBox_TextChanged(this, null!);
        }

        /// <summary>
        /// Walks each <see cref="TextPointer"/> in the current selection and returns <c>true</c>
        /// if any <see cref="Inline"/> or <see cref="Paragraph"/> element has a non-transparent
        /// <see cref="SolidColorBrush"/> background.
        /// <para>
        /// Using <c>TextSelection.GetPropertyValue</c> alone is unreliable for mixed-format selections
        /// (e.g. text loaded from RTF), so we scan individual runs instead.
        /// </para>
        /// </summary>
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

        private void ContentRichTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Let the RichTextBox handle zoom if Ctrl is pressed
                return;
            }

            if (sender is System.Windows.Controls.RichTextBox rtb)
            {
                var scrollViewer = GetDescendantByType<ScrollViewer>(rtb);
                if (scrollViewer != null)
                {
                    e.Handled = true;
                    // Windows default scroll delta is typically 120. 
                    // Slow it down to ~30% of normal physical scrolling speed for a smoother, more controlled feel in a small window.
                    double scrollMultiplier = 0.3;
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta * scrollMultiplier));
                }
            }
        }

        private void ContentRichTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (NoteModel.IsLocked) return;

            var pos = e.GetPosition(ContentRichTextBox);
            var pointer = ContentRichTextBox.GetPositionFromPoint(pos, true);
            if (pointer != null)
            {
                var fwdChar = GetCharFromPointer(pointer, LogicalDirection.Forward);
                if (fwdChar == '☐' || fwdChar == '☑')
                {
                    var tr = new TextRange(pointer, pointer.GetPositionAtOffset(1, LogicalDirection.Forward));
                    tr.Text = fwdChar == '☐' ? "☑" : "☐";
                    e.Handled = true;
                    return;
                }
                
                var bwdChar = GetCharFromPointer(pointer, LogicalDirection.Backward);
                if (bwdChar == '☐' || bwdChar == '☑')
                {
                    var tr = new TextRange(pointer.GetPositionAtOffset(-1, LogicalDirection.Backward), pointer);
                    tr.Text = bwdChar == '☐' ? "☑" : "☐";
                    e.Handled = true;
                    return;
                }
            }
        }

        private char GetCharFromPointer(TextPointer pointer, LogicalDirection dir)
        {
            if (pointer.GetPointerContext(dir) == TextPointerContext.Text)
            {
                string text = pointer.GetTextInRun(dir);
                if (!string.IsNullOrEmpty(text))
                {
                    return dir == LogicalDirection.Forward ? text[0] : text[text.Length - 1];
                }
            }
            return '\0';
        }

        private void ContentRichTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (NoteModel.IsLocked) return;

            if (e.Key == Key.Enter)
            {
                bool processed = ProcessMarkdownAtCaret(Key.Enter, out bool skipEnterBreak);

                e.Handled = true;

                if (processed && skipEnterBreak)
                {
                    // The markdown was a line-prefix (like - or #), so stay on the same line to type the content.
                    return;
                }
                
                // Let WPF create the new paragraph or list item
                EditingCommands.EnterParagraphBreak.Execute(null, ContentRichTextBox);
                
                // Clear paragraph-level formatting (like large Fonts from Headers)
                var caret = ContentRichTextBox.CaretPosition;
                if (caret.Paragraph != null)
                {
                    caret.Paragraph.ClearValue(TextElement.FontSizeProperty);
                    caret.Paragraph.ClearValue(TextElement.FontWeightProperty);
                    caret.Paragraph.ClearValue(TextElement.FontStyleProperty);
                }

                // Reset inline typing state (so bold, italic, strikethrough don't bleed)
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                ContentRichTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                
                return;
            }

            if (e.Key == Key.Space)
            {
                if (ProcessMarkdownAtCaret(Key.Space, out _))
                {
                    e.Handled = true;
                }
            }
        }

        private bool ProcessMarkdownAtCaret(Key triggerKey, out bool skipEnterBreak)
        {
            skipEnterBreak = false;
            
            var caret = ContentRichTextBox.CaretPosition;
            var currentParagraph = caret.Paragraph;
            if (currentParagraph == null) return false;

            var startOfLine = currentParagraph.ContentStart;
            var textRangeBeforeCaret = new TextRange(startOfLine, caret);
            var textBeforeCaret = textRangeBeforeCaret.Text;

            // 1. Lists
            if (textBeforeCaret == "-" || textBeforeCaret == "*")
            {
                textRangeBeforeCaret.Text = "";
                EditingCommands.ToggleBullets.Execute(null, ContentRichTextBox);
                skipEnterBreak = true;
                return true;
            }
            if (textBeforeCaret == "1.")
            {
                textRangeBeforeCaret.Text = "";
                EditingCommands.ToggleNumbering.Execute(null, ContentRichTextBox);
                skipEnterBreak = true;
                return true;
            }

            // 2. Checkboxes
            if (textBeforeCaret == "[]")
            {
                textRangeBeforeCaret.Text = "☐ ";
                ContentRichTextBox.CaretPosition = textRangeBeforeCaret.End;
                skipEnterBreak = true;
                return true;
            }
            if (textBeforeCaret.ToLower() == "[x]")
            {
                textRangeBeforeCaret.Text = "☑ ";
                ContentRichTextBox.CaretPosition = textRangeBeforeCaret.End;
                skipEnterBreak = true;
                return true;
            }

            // 3. Headers
            if (textBeforeCaret == "#" || textBeforeCaret == "##" || textBeforeCaret == "###")
            {
                textRangeBeforeCaret.Text = "";
                
                double size = textBeforeCaret == "#" ? 28.0 : textBeforeCaret == "##" ? 22.0 : 18.0;
                currentParagraph.FontSize = size;
                currentParagraph.FontWeight = FontWeights.Bold;
                
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                
                skipEnterBreak = true;
                return true;
            }

            // 4. Horizontal Rule
            if (textBeforeCaret == "---")
            {
                textRangeBeforeCaret.Text = "────────────────────────────────────────";
                ContentRichTextBox.CaretPosition = textRangeBeforeCaret.End;
                // Do not skip enter break here, we want the cursor below the horizontal line
                return true;
            }

            // 5. Inline Formatting
            if (TryApplyInlineFormatting(caret, textBeforeCaret, "**", TextElement.FontWeightProperty, FontWeights.Bold, triggerKey)) return true;
            if (TryApplyInlineFormatting(caret, textBeforeCaret, "*", TextElement.FontStyleProperty, FontStyles.Italic, triggerKey)) return true;
            if (TryApplyInlineFormatting(caret, textBeforeCaret, "~~", Inline.TextDecorationsProperty, TextDecorations.Strikethrough, triggerKey)) return true;
            if (TryApplyInlineFormatting(caret, textBeforeCaret, "==", TextElement.BackgroundProperty, System.Windows.Media.Brushes.Yellow, triggerKey)) return true;

            // 6. Hyperlinks
            if (TryApplyHyperlink(caret, textBeforeCaret, triggerKey)) return true;

            // 7. Text Replacements
            if (TryApplyTextReplacement(caret, textBeforeCaret, "--->", "⟶", triggerKey)) return true;
            if (TryApplyTextReplacement(caret, textBeforeCaret, "-->", "→", triggerKey)) return true;
            if (TryApplyTextReplacement(caret, textBeforeCaret, "==>", "⇒", triggerKey)) return true;

            return false;
        }

        private bool TryApplyTextReplacement(TextPointer caret, string textBeforeCaret, string matchText, string replacementText, Key triggerKey)
        {
            if (textBeforeCaret.EndsWith(matchText))
            {
                TextPointer? startFormat = GetPointerAtBackwardOffset(caret, matchText.Length);
                if (startFormat != null)
                {
                    var formatRange = new TextRange(startFormat, caret);
                    formatRange.Text = replacementText;

                    ContentRichTextBox.CaretPosition = formatRange.End;
                    
                    if (triggerKey == Key.Space)
                    {
                        ContentRichTextBox.CaretPosition.InsertTextInRun(" ");
                        ContentRichTextBox.CaretPosition = ContentRichTextBox.CaretPosition.GetPositionAtOffset(1, LogicalDirection.Forward) ?? ContentRichTextBox.CaretPosition;
                    }
                    return true;
                }
            }
            return false;
        }

        private bool TryApplyInlineFormatting(TextPointer caret, string textBeforeCaret, string delimiter, DependencyProperty property, object value, Key triggerKey)
        {
            if (!textBeforeCaret.EndsWith(delimiter)) return false;

            int lastMatch = textBeforeCaret.LastIndexOf(delimiter, textBeforeCaret.Length - 1 - delimiter.Length, StringComparison.Ordinal);
            if (lastMatch >= 0)
            {
                int lengthToReplace = textBeforeCaret.Length - lastMatch;
                TextPointer? startFormat = GetPointerAtBackwardOffset(caret, lengthToReplace);
                if (startFormat != null)
                {
                    var formatRange = new TextRange(startFormat, caret);
                    string content = formatRange.Text;
                    
                    if (content.StartsWith(delimiter) && content.EndsWith(delimiter) && content.Length > delimiter.Length * 2)
                    {
                        var leadingRange = new TextRange(startFormat, startFormat.GetPositionAtOffset(delimiter.Length, LogicalDirection.Forward));
                        leadingRange.Text = ""; 
                        
                        TextPointer? newEnd = GetPointerAtBackwardOffset(caret, delimiter.Length);
                        if (newEnd != null)
                        {
                            var trailingRange = new TextRange(newEnd, caret);
                            trailingRange.Text = ""; 
                            
                            var contentRange = new TextRange(startFormat, newEnd);
                            contentRange.ApplyPropertyValue(property, value);
                            
                            ContentRichTextBox.CaretPosition = contentRange.End;
                            
                            if (triggerKey == Key.Space)
                            {
                                ContentRichTextBox.CaretPosition.InsertTextInRun(" ");
                                ContentRichTextBox.CaretPosition = ContentRichTextBox.CaretPosition.GetPositionAtOffset(1, LogicalDirection.Forward) ?? ContentRichTextBox.CaretPosition;
                            }
                            
                            var spaceRange = new TextRange(contentRange.End, ContentRichTextBox.CaretPosition);
                            spaceRange.ApplyPropertyValue(property, DependencyProperty.UnsetValue);
                            
                            if (property == TextElement.FontStyleProperty)
                            {
                                spaceRange.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                            }
                            else if (property == TextElement.FontWeightProperty)
                            {
                                spaceRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                            }
                            else if (property == Inline.TextDecorationsProperty)
                            {
                                spaceRange.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection());
                            }
                            else if (property == TextElement.BackgroundProperty)
                            {
                                spaceRange.ApplyPropertyValue(TextElement.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                            }
                            
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool TryApplyHyperlink(TextPointer caret, string textBeforeCaret, Key triggerKey)
        {
            var match = System.Text.RegularExpressions.Regex.Match(textBeforeCaret, @"\[([^\]]+)\]\(([^)]+)\)$");
            if (match.Success)
            {
                int lengthToReplace = match.Length;
                TextPointer? startFormat = GetPointerAtBackwardOffset(caret, lengthToReplace);
                if (startFormat != null)
                {
                    var formatRange = new TextRange(startFormat, caret);
                    formatRange.Text = ""; // Clear markdown text

                    string linkText = match.Groups[1].Value;
                    string linkUrl = match.Groups[2].Value;
                    
                    if (!linkUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !linkUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                        !linkUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        linkUrl = "https://" + linkUrl;
                    }

                    Hyperlink hyperlink = new Hyperlink(new Run(linkText), startFormat);
                    hyperlink.ToolTip = "Ctrl+Click to follow link";
                    
                    try
                    {
                        hyperlink.NavigateUri = new Uri(linkUrl, UriKind.Absolute);
                    }
                    catch { /* Ignore invalid URI formats */ }

                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
                        }
                        catch { }
                        args.Handled = true;
                    };

                    ContentRichTextBox.CaretPosition = hyperlink.ElementEnd;

                    if (triggerKey == Key.Space)
                    {
                        ContentRichTextBox.CaretPosition.InsertTextInRun(" ");
                        ContentRichTextBox.CaretPosition = ContentRichTextBox.CaretPosition.GetPositionAtOffset(1, LogicalDirection.Forward) ?? ContentRichTextBox.CaretPosition;
                    }

                    // Reset inline formatting so subsequent text isn't treated as part of the link
                    ContentRichTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                    ContentRichTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, System.Windows.Media.Brushes.Black);

                    return true;
                }
            }
            return false;
        }

        private TextPointer? GetPointerAtBackwardOffset(TextPointer pointer, int charCount)
        {
            TextPointer? current = pointer;
            int charsFound = 0;
            while (current != null && charsFound < charCount)
            {
                if (current.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text)
                {
                    int runLength = current.GetTextInRun(LogicalDirection.Backward).Length;
                    if (charsFound + runLength >= charCount)
                    {
                        return current.GetPositionAtOffset(-(charCount - charsFound), LogicalDirection.Backward);
                    }
                    charsFound += runLength;
                }
                current = current.GetNextContextPosition(LogicalDirection.Backward);
            }
            return current;
        }

        private static T? GetDescendantByType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T result)
                {
                    return result;
                }

                T? childItem = GetDescendantByType<T>(child);
                if (childItem != null)
                {
                    return childItem;
                }
            }
            return null;
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

        /// <summary>
        /// Handles window-level keyboard shortcuts that must intercept input before the
        /// <see cref="System.Windows.Controls.RichTextBox"/> processes it.
        /// <list type="table">
        ///   <listheader><term>Key</term><description>Action</description></listheader>
        ///   <item><term>Ctrl+H</term><description>Toggle highlight on selection</description></item>
        ///   <item><term>Ctrl+Shift+L</term><description>Toggle note lock</description></item>
        ///   <item><term>Ctrl+Shift+X</term><description>Toggle strikethrough on selection</description></item>
        ///   <item><term>Ctrl+T</term><description>Insert timestamp at caret</description></item>
        ///   <item><term>Ctrl+N</term><description>Create a new note</description></item>
        /// </list>
        /// </summary>
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
                TitleBlock.Cursor = System.Windows.Input.Cursors.Arrow;
                FormatPopup.IsOpen = false;
            }
            else
            {
                // Show unlocked Padlock (Solid with keyhole, left hanger swung open, even length)
                LockIconPath.Data = Geometry.Parse("M 19 9 L 5 9 C 3.9 9 3 9.9 3 11 L 3 20 C 3 21.1 3.9 22 5 22 L 19 22 C 20.1 22 21 21.1 21 20 L 21 11 C 21 9.9 20.1 9 19 9 Z M 10 9 L 10 6 C 10 2.7 7.3 0 4 0 C 0.7 0 -2 2.7 -2 6 L -2 9 C -2 10.1 -0.9 11 0 11 C 0.9 11 2 10.1 2 9 L 2 6 C 2 4.9 2.9 4 4 4 C 5.1 4 6 4.9 6 6 L 6 9 Z M 12 17 A 1.5 1.5 0 0 0 12.75 14.33 L 12.75 12 A 0.75 0.75 0 0 0 11.25 12 L 11.25 14.33 A 1.5 1.5 0 0 0 12 17 Z");
                LockNoteBtn.ToolTip = "Lock Note";
                
                ContentRichTextBox.IsReadOnly = false;
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

        private void NoteWindow_LocationChanged(object? sender, EventArgs e)
        {
            UpdatePinWindowPosition();
        }

        private void NoteWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePinWindowPosition();
        }

        private void UpdatePinWindowPosition()
        {
            if (_pinWindow != null && ClickThroughBtn.IsVisible)
            {
                var pt = ClickThroughBtn.TranslatePoint(new System.Windows.Point(0, 0), this);
                _pinWindow.Left = this.Left + pt.X;
                _pinWindow.Top = this.Top + pt.Y;
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

        // --- Click-Through ---

        /// <summary>
        /// Synchronises the UI (handle button visibility, context menu label, Topmost, desktop
        /// detach) to the current value of <see cref="NoteModel.IsClickThrough"/>.
        /// Call after toggling <see cref="NoteModel.IsClickThrough"/> and on startup.
        /// Public so <see cref="NoteManagerWindow"/> can reach it as a fallback.
        /// </summary>
        public void ApplyClickThroughState()
        {
            MenuItem? clickThroughMenuItem = null;
            if (TryFindResource("NoteContextMenu") is ContextMenu cm)
            {
                foreach (var item in cm.Items)
                {
                    if (item is MenuItem mi && (mi.Header?.ToString() == "Enable Click-Through" || mi.Header?.ToString() == "Disable Click-Through"))
                    {
                        clickThroughMenuItem = mi;
                        break;
                    }
                }
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int style = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (NoteModel.IsClickThrough)
            {
                // Force always-on-top visually so the note stays visible over other apps,
                // without modifying the persisted NoteModel.IsAlwaysOnTop value.
                Topmost = true;
                
                // Safely add WS_EX_TRANSPARENT without stripping WS_EX_LAYERED
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);

                if (_pinWindow == null)
                {
                    _pinWindow = new PinWindow(() => 
                    {
                        NoteModel.IsClickThrough = false;
                        ApplyClickThroughState();
                        NoteModel.UpdatedAt = DateTime.Now;
                        NoteChanged?.Invoke(this, EventArgs.Empty);
                        ShowSavedIndicator();
                    });
                    _pinWindow.Owner = this;
                    
                    // Position exactly over ClickThroughBtn
                    var pt = ClickThroughBtn.TranslatePoint(new System.Windows.Point(0, 0), this);
                    _pinWindow.Left = this.Left + pt.X;
                    _pinWindow.Top = this.Top + pt.Y;
                    _pinWindow.Width = ClickThroughBtn.ActualWidth > 0 ? ClickThroughBtn.ActualWidth : 28;
                    _pinWindow.Height = ClickThroughBtn.ActualHeight > 0 ? ClickThroughBtn.ActualHeight : 28;

                    _pinWindow.Show();
                }

                if (clickThroughMenuItem != null)
                {
                    clickThroughMenuItem.Header = "Disable Click-Through";
                }
                AlwaysOnTopChanged?.Invoke(this, EventArgs.Empty); // detach from desktop layer
            }
            else
            {
                // Remove WS_EX_TRANSPARENT
                SetWindowLong(hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);

                // Restore Topmost to whatever the user originally had
                Topmost = NoteModel.IsAlwaysOnTop;

                if (_pinWindow != null)
                {
                    _pinWindow.Close();
                    _pinWindow = null;
                }

                if (clickThroughMenuItem != null)
                {
                    clickThroughMenuItem.Header = "Enable Click-Through";
                }

                // Fire event so App.xaml.cs re-attaches to desktop (sets owner + HWND_BOTTOM)
                AlwaysOnTopChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Toggles click-through on/off from the context menu item.
        /// </summary>
        private void ClickThroughMenu_Click(object sender, RoutedEventArgs e)
        {
            NoteModel.IsClickThrough = !NoteModel.IsClickThrough;
            ApplyClickThroughState();
            NoteModel.UpdatedAt = DateTime.Now;
            NoteChanged?.Invoke(this, EventArgs.Empty);
            ShowSavedIndicator();
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

        // --- Context Menu Checkmarks ---

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                // Find OpacityMenuItem
                var opacityMenuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "OpacityMenuItem");
                if (opacityMenuItem != null)
                {
                    foreach (var item in opacityMenuItem.Items.OfType<MenuItem>())
                    {
                        if (item.Tag is string opacityStr && double.TryParse(opacityStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double opacityVal))
                        {
                            item.IsChecked = Math.Abs(opacityVal - NoteModel.Opacity) < 0.01;
                        }
                    }
                }
            }
        }

        // --- Settings ---
        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            RequestSettings?.Invoke(this, EventArgs.Empty);
        }

        private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newWidth = this.Width + e.HorizontalChange;
            double newHeight = this.Height + e.VerticalChange;
            
            if (newWidth >= this.MinWidth)
                this.Width = newWidth;
                
            if (newHeight >= this.MinHeight)
                this.Height = newHeight;
        }
    }
}
