using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DesktopNotes.Services;

namespace DesktopNotes.Views
{
    public partial class ImageViewerWindow : Window
    {
        private BitmapSource? _bitmap;
        private string _imagePath = string.Empty;
        private double _zoomFactor = 1.0;
        private readonly System.Threading.CancellationTokenSource _load = new();
        private System.Windows.Point? _panStart;
        private double _panX, _panY;

        private void InitializeLifecycle()
        {
            Closed += (_, _) => { _load.Cancel(); ViewerImage.Source = null; _bitmap = null; };
            ImageContainer.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _panStart = e.GetPosition(ImageScrollViewer);
                _panX = ImageScrollViewer.HorizontalOffset; _panY = ImageScrollViewer.VerticalOffset;
                ImageScrollViewer.CaptureMouse(); e.Handled = true;
            };
            ImageScrollViewer.PreviewMouseMove += (_, e) =>
            {
                if (_panStart is not System.Windows.Point start || e.LeftButton != MouseButtonState.Pressed) return;
                var current = e.GetPosition(ImageScrollViewer);
                ImageScrollViewer.ScrollToHorizontalOffset(_panX - current.X + start.X);
                ImageScrollViewer.ScrollToVerticalOffset(_panY - current.Y + start.Y);
            };
            ImageScrollViewer.PreviewMouseLeftButtonUp += (_, e) => { _panStart = null; ImageScrollViewer.ReleaseMouseCapture(); };
        }

        private void ViewerSurface_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Border.ClipToBounds only clips to a rectangle. Clip the header and
            // footer to the inner rounded corners while keeping the outer stroke visible.
            var surface = (FrameworkElement)sender;
            surface.Clip = new System.Windows.Media.RectangleGeometry(new Rect(e.NewSize), 9, 9);
        }

        public ImageViewerWindow(string imagePath, string title = "Image Viewer")
        {
            InitializeComponent();
            InitializeLifecycle();
            _imagePath = imagePath;
            TitleTextBlock.Text = Path.GetFileName(imagePath);

            LoadImage(imagePath);
        }

        public ImageViewerWindow(BitmapSource bitmap, string title = "Image Viewer")
        {
            InitializeComponent();
            InitializeLifecycle();
            _bitmap = bitmap;
            TitleTextBlock.Text = title;

            SetBitmap(bitmap);
        }

        private async void LoadImage(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                var bi = await ImageDecodeService.LoadAsync(path, 0, _load.Token);
                if (_load.IsCancellationRequested) return;
                _bitmap = bi;
                SetBitmap(bi);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetBitmap(BitmapSource bitmap)
        {
            ViewerImage.Source = bitmap;
            ResolutionTextBlock.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px";

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(FitToScreen));
        }

        private void ApplyZoom(double newZoom)
        {
            _zoomFactor = Math.Max(0.005, Math.Min(5.0, newZoom));
            ImageScaleTransform.ScaleX = _zoomFactor;
            ImageScaleTransform.ScaleY = _zoomFactor;
            ZoomPercentText.Text = $"{(int)(_zoomFactor * 100)}%";
        }

        private void FitToScreen()
        {
            if (_bitmap == null) return;

            double viewWidth = ImageScrollViewer.ActualWidth > 50 ? ImageScrollViewer.ActualWidth - 30 : 700;
            double viewHeight = ImageScrollViewer.ActualHeight > 50 ? ImageScrollViewer.ActualHeight - 30 : 500;

            double scaleX = viewWidth / _bitmap.Width;
            double scaleY = viewHeight / _bitmap.Height;
            double fitScale = Math.Min(scaleX, scaleY);

            // Don't upscale small images beyond 100% on initial fit
            if (fitScale > 1.0) fitScale = 1.0;

            ApplyZoom(fitScale);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Close on click outside if desired, or allow drag
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoom(_zoomFactor * 1.25);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoom(_zoomFactor / 1.25);
        }

        private void ActualSize_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoom(1.0);
        }

        private void FitToScreen_Click(object sender, RoutedEventArgs e)
        {
            FitToScreen();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    ApplyZoom(_zoomFactor * 1.15);
                }
                else if (e.Delta < 0)
                {
                    ApplyZoom(_zoomFactor / 1.15);
                }
                e.Handled = true;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_bitmap != null)
            {
                try
                {
                    System.Windows.Clipboard.SetImage(_bitmap);
                    CopyBtn.ToolTip = "Copied!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s, args) =>
                    {
                        CopyBtn.ToolTip = "Copy Image (Ctrl+C)";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch { }
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Image",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                FileName = "Image.png",
                DefaultExt = ".png"
            };

            if (sfd.ShowDialog(this) == true)
            {
                try
                {
                    ImageExportService.Export(_bitmap, sfd.FileName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                Copy_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                SaveAs_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                ZoomIn_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                ZoomOut_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
