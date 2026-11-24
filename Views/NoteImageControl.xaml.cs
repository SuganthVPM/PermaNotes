using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopNotes.Models;
using DesktopNotes.Services;
using UserControl = System.Windows.Controls.UserControl;
using RichTextBox = System.Windows.Controls.RichTextBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DesktopNotes.Views;

public partial class NoteImageControl : UserControl
{
    public NoteImageReference Reference { get; private set; } = new("missing.png", .3, Guid.NewGuid());
    public string FileName => Reference.FileName;
    public string FullPath { get; private set; } = "";
    public double WidthRatio => Reference.WidthRatio;
    public BitmapSource? ImageBitmap => DisplayImage.Source as BitmapSource;
    public bool CanEdit { get; set; } = true;
    public event EventHandler? DoubleClicked;
    public event EventHandler? ResizeStarted;
    public event EventHandler? Resized;
    private CancellationTokenSource? _decode;
    private int _requestedWidth;
    private bool _deferThumbnailRefresh;
    private readonly DispatcherTimer _thumbnailRelease;

    public NoteImageControl()
    {
        InitializeComponent();
        // FlowDocument can briefly detach inline images when its lines reflow.
        // Keep the current bitmap through that transition, but release hidden notes' thumbnails.
        _thumbnailRelease = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _thumbnailRelease.Tick += (_, _) =>
        {
            _thumbnailRelease.Stop();
            if (_deferThumbnailRefresh || (IsLoaded && IsVisible)) return;
            ReleaseThumbnail();
        };
        Loaded += (_, _) => ResumeThumbnail();
        IsVisibleChanged += (_, _) =>
        {
            if (IsLoaded && IsVisible) ResumeThumbnail();
            else ScheduleThumbnailRelease();
        };
        Unloaded += (_, _) => ScheduleThumbnailRelease();
        ResizeThumb.DragStarted += (_, _) => { if (CanEdit) ResizeStarted?.Invoke(this, EventArgs.Empty); };
        System.Windows.Automation.AutomationProperties.SetName(this, "Note image. Double-click to view or right-click for image actions.");
    }

    internal void ReleaseThumbnail()
    {
        _thumbnailRelease.Stop();
        _decode?.Cancel();
        DisplayImage.Source = null;
        _requestedWidth = 0;
    }

    private void ResumeThumbnail()
    {
        if (!IsVisible) return;
        _thumbnailRelease.Stop();
        if (!_deferThumbnailRefresh || DisplayImage.Source == null) RefreshThumbnail();
    }

    private void ScheduleThumbnailRelease()
    {
        _thumbnailRelease.Stop();
        _thumbnailRelease.Start();
    }

    public void LoadReference(string path, NoteImageReference reference, double viewportWidth)
    {
        _decode?.Cancel();
        _requestedWidth = 0;
        DisplayImage.Source = null;
        Reference = reference; FullPath = path;
        ToolTip = System.IO.File.Exists(path) ? "Double-click to view. Right-click for image actions." : "Image unavailable. Right-click to retry or relink.";
        UpdateResponsiveWidth(viewportWidth);
    }
    public void UpdateResponsiveWidth(double viewportWidth, bool refreshThumbnail = true)
    {
        if (viewportWidth <= 0) return;
        bool wasDeferred = _deferThumbnailRefresh;
        _deferThumbnailRefresh = !refreshThumbnail;
        Width = Math.Clamp(viewportWidth * WidthRatio, Math.Min(70, viewportWidth), Math.Max(70, viewportWidth - 32));
        ResizeThumb.Visibility = CanEdit ? Visibility.Visible : Visibility.Collapsed;
        if (!refreshThumbnail) return;
        if (IsLoaded && IsVisible) ResumeThumbnail();
        else if (wasDeferred) ScheduleThumbnailRelease();
    }
    public void SetWidthRatio(double ratio, double viewportWidth)
    {
        if (!CanEdit) return;
        ResizeStarted?.Invoke(this, EventArgs.Empty);
        Reference = Reference with { WidthRatio = Math.Clamp(ratio, .15, 1) };
        UpdateResponsiveWidth(viewportWidth); Resized?.Invoke(this, EventArgs.Empty);
    }
    public async void RefreshThumbnail()
    {
        if (!System.IO.File.Exists(FullPath))
        {
            _decode?.Cancel();
            _requestedWidth = 0;
            DisplayImage.Source = null;
            MissingLabel.Visibility = Visibility.Visible;
            return;
        }
        double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        int pixels = ImageDecodeService.NotePreviewWidth((double.IsNaN(Width) ? 100 : Width) * dpi);
        if (pixels == _requestedWidth &&
            (_decode is { IsCancellationRequested: false } || DisplayImage.Source != null)) return;
        _decode?.Cancel();
        var request = _decode = new CancellationTokenSource(); _requestedWidth = pixels;
        try
        {
            var bitmap = await ImageDecodeService.LoadAsync(FullPath, pixels, request.Token);
            if (request.IsCancellationRequested) return;
            DisplayImage.Source = bitmap; MissingLabel.Visibility = Visibility.Collapsed;
            ToolTip = "Double-click to view. Right-click for image actions.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!request.IsCancellationRequested)
            {
                _requestedWidth = 0;
                if (DisplayImage.Source == null) MissingLabel.Visibility = Visibility.Visible;
                ToolTip = "Image unavailable: " + ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_decode, request)) _decode = null;
            request.Dispose();
        }
    }
    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanEdit) return;
        double viewport = FindVisualParent<RichTextBox>(this)?.ActualWidth ?? 300;
        double width = Math.Clamp(Width + e.HorizontalChange, 70, Math.Max(70, viewport - 32));
        Reference = Reference with { WidthRatio = Math.Clamp(width / viewport, .15, 1) }; Width = width;
    }
    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    { if (CanEdit) { RefreshThumbnail(); Resized?.Invoke(this, EventArgs.Empty); } }
    private void RootGrid_MouseEnter(object sender, MouseEventArgs e) => HoverBorder.Visibility = Visibility.Visible;
    private void RootGrid_MouseLeave(object sender, MouseEventArgs e) => HoverBorder.Visibility = Visibility.Collapsed;
    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) DoubleClicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null) { if (child is T parent) return parent; child = System.Windows.Media.VisualTreeHelper.GetParent(child); }
        return null;
    }
}
