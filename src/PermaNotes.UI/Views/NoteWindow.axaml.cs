using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PermaNotes.Core.Services;
using PermaNotes.UI.ViewModels;

namespace PermaNotes.UI.Views
{
    public partial class NoteWindow : Window
    {
        // Header height: 10px window margin + 36px HeaderPanel
        private const double HeaderExemptHeight = 46.0;

        private bool _isInitializing = true;

        private readonly IClickThroughService? _clickThroughService;
        private readonly IDesktopPinService?   _desktopPinService;
        private readonly IVibrancyService?     _vibrancyService;

        public event EventHandler? RequestNewNote;
        public event EventHandler? RequestOpenManager;
        public event EventHandler? RequestDuplicateNote;
        public event EventHandler? RequestDeleteNote;

        public NoteWindow()
        {
            InitializeComponent();

            Loaded += NoteWindow_Loaded;
            PositionChanged += NoteWindow_PositionChanged;
            SizeChanged += NoteWindow_SizeChanged;
        }

        public NoteWindow(
            NoteViewModel viewModel,
            IClickThroughService? clickThroughService = null,
            IDesktopPinService?   desktopPinService   = null,
            IVibrancyService?     vibrancyService     = null) : this()
        {
            DataContext = viewModel;
            _clickThroughService = clickThroughService;
            _desktopPinService   = desktopPinService;
            _vibrancyService     = vibrancyService;
        }

        private void NoteWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is NoteViewModel vm)
            {
                // Restore window geometry
                if (vm.X > 0 || vm.Y > 0)
                {
                    Position = new PixelPoint((int)vm.X, (int)vm.Y);
                }
                if (vm.Width > 0) Width = vm.Width;
                if (vm.Height > 0) Height = vm.Height;

                // Load content
                if (!string.IsNullOrEmpty(vm.RtfText))
                {
                    try
                    {
                        ContentRichTextBox.LoadRtf(vm.RtfText);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load RTF content: {ex.Message}");
                    }
                }
                else if (!string.IsNullOrEmpty(vm.Text))
                {
                    try
                    {
                        ContentRichTextBox.InsertText(vm.Text);
                    }
                    catch { }
                }

                ContentRichTextBox.IsReadOnly = vm.IsLocked;

                // Track document changes to persist RTF
                ContentRichTextBox.DocumentChanged += ContentRichTextBox_DocumentChanged;

                if (vm.IsGlassmorphic)
                {
                    _vibrancyService?.SetVibrancy(this, true);
                }

                if (!vm.IsAlwaysOnTop)
                {
                    _desktopPinService?.Attach(this);
                }

                vm.PropertyChanged += ViewModel_PropertyChanged;
            }

            _isInitializing = false;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (DataContext is not NoteViewModel vm) return;

            if (e.PropertyName == nameof(NoteViewModel.IsLocked))
            {
                ContentRichTextBox.IsReadOnly = vm.IsLocked;
            }
            else if (e.PropertyName == nameof(NoteViewModel.IsClosed))
            {
                if (vm.IsClosed) Hide();
                else             Show();
            }
            else if (e.PropertyName == nameof(NoteViewModel.IsAlwaysOnTop))
            {
                Topmost = vm.IsAlwaysOnTop;
                if (vm.IsAlwaysOnTop)
                {
                    _desktopPinService?.Detach(this);
                }
                else
                {
                    _desktopPinService?.Attach(this);
                }
            }
            else if (e.PropertyName == nameof(NoteViewModel.IsClickThrough))
            {
                ApplyClickThrough(vm);
            }
            else if (e.PropertyName == nameof(NoteViewModel.IsGlassmorphic))
            {
                _vibrancyService?.SetVibrancy(this, vm.IsGlassmorphic);
            }
        }

        /// <summary>
        /// Tells the platform service to enable or disable click-through for this window.
        /// The header row (HeaderExemptHeight px from top) is always kept interactive so
        /// the drag bar and icon buttons remain clickable.
        /// </summary>
        private void ApplyClickThrough(NoteViewModel vm)
        {
            if (_clickThroughService == null) return;

            _clickThroughService.SetClickThrough(
                this,
                vm.IsClickThrough,
                exemptRegion: (0, 0, Width, HeaderExemptHeight));
        }

        private void ContentRichTextBox_DocumentChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            if (DataContext is NoteViewModel vm && !vm.IsLocked)
            {
                try
                {
                    vm.RtfText = ContentRichTextBox.SaveRtf();
                    vm.Text = ContentRichTextBox.FlowDocument.Text;
                    ShowSavedIndicator();
                }
                catch { }
            }
        }

        private async void ShowSavedIndicator()
        {
            try
            {
                SavedIndicator.Opacity = 0.8;
                await System.Threading.Tasks.Task.Delay(1200);
                SavedIndicator.Opacity = 0.0;
            }
            catch { }
        }

        private void NoteWindow_PositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (_isInitializing) return;
            if (DataContext is NoteViewModel vm)
            {
                vm.X = e.Point.X;
                vm.Y = e.Point.Y;
            }
        }

        private void NoteWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (DataContext is NoteViewModel vm)
            {
                vm.Width = Bounds.Width;
                vm.Height = Bounds.Height;
            }
        }

        private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is NoteViewModel vm && vm.IsLocked) return;

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is NoteViewModel vm && vm.IsLocked) return;

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
            }
        }

        private void Title_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (DataContext is NoteViewModel vm && !vm.IsLocked)
            {
                vm.IsEditingTitle = true;
                TitleEditBox.Focus();
                TitleEditBox.SelectAll();
            }
        }

        private void TitleEdit_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is NoteViewModel vm)
            {
                vm.IsEditingTitle = false;
            }
        }

        private void TitleEdit_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is NoteViewModel vm)
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    vm.IsEditingTitle = false;
                    e.Handled = true;
                }
            }
        }

        // Formatting actions
        private void Bold_Click(object? sender, RoutedEventArgs e)
        {
            ContentRichTextBox.ToggleBold();
        }

        private void Italic_Click(object? sender, RoutedEventArgs e)
        {
            ContentRichTextBox.ToggleItalics();
        }

        private void Underline_Click(object? sender, RoutedEventArgs e)
        {
            ContentRichTextBox.ToggleUnderlining();
        }

        private void Strikethrough_Click(object? sender, RoutedEventArgs e)
        {
            ContentRichTextBox.ToggleStrikethrough();
        }

        private void Timestamp_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is NoteViewModel vm && vm.IsLocked) return;

            var timestamp = DateTime.Now.ToString("g") + " ";
            ContentRichTextBox.InsertText(timestamp);
        }

        // Clipboard
        private void Cut_Click(object? sender, RoutedEventArgs e)
        {
            // Handled natively by keydown or selection
        }

        private void Copy_Click(object? sender, RoutedEventArgs e)
        {
        }

        private void Paste_Click(object? sender, RoutedEventArgs e)
        {
        }

        // Window actions
        private void NewNote_Click(object? sender, RoutedEventArgs e)
        {
            RequestNewNote?.Invoke(this, EventArgs.Empty);
        }

        private void OpenNoteManager_Click(object? sender, RoutedEventArgs e)
        {
            RequestOpenManager?.Invoke(this, EventArgs.Empty);
        }

        private void DuplicateNote_Click(object? sender, RoutedEventArgs e)
        {
            RequestDuplicateNote?.Invoke(this, EventArgs.Empty);
        }

        private void DeleteNote_Click(object? sender, RoutedEventArgs e)
        {
            RequestDeleteNote?.Invoke(this, EventArgs.Empty);
        }

        private async void CustomColor_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not NoteViewModel vm) return;

            var dialog = new ColorWheelDialog(vm.BackgroundColor);
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                vm.BackgroundColor = result;
            }
        }

        private void FontSize_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string sizeStr && double.TryParse(sizeStr, out double size))
            {
                if (ContentRichTextBox.FlowDocument != null && ContentRichTextBox.FlowDocument.Selection.Length > 0)
                {
                    ContentRichTextBox.FlowDocument.Selection.ApplyFormatting(Avalonia.Controls.Documents.Inline.FontSizeProperty, size);
                }
                else
                {
                    ContentRichTextBox.FontSize = size;
                }
            }
        }

        private void CloseNote_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is NoteViewModel vm)
            {
                vm.IsClosed = true;
            }
            Hide();
        }
    }
}
