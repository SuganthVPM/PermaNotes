using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopNotes.Views
{
    public partial class ColorWheelDialog : Window
    {
        // ── Public result ──────────────────────────────────────────────
        public string? SelectedHexColor { get; private set; }

        // ── Internal state ─────────────────────────────────────────────
        private double _hue        = 0;   // 0–360
        private double _saturation = 0;   // 0–1
        private double _brightness = 1;   // 0–1 (Value in HSV)

        private bool _isDraggingWheel = false;
        private bool _suppressHexSync = false;
        private bool _isLoaded        = false;

        // Wheel is drawn to fill the canvas — computed at render time
        private double WheelRadius   => Math.Min(WheelCanvas.ActualWidth, WheelCanvas.ActualHeight) / 2.0 - 4;
        private double WheelCenterX  => WheelCanvas.ActualWidth  / 2.0;
        private double WheelCenterY  => WheelCanvas.ActualHeight / 2.0;

        public ColorWheelDialog(string? initialHexColor = null)
        {
            InitializeComponent();

            // Set slider in code so ValueChanged doesn't fire before controls exist
            _suppressHexSync = true;
            BrightnessSlider.Value = 1.0;
            _suppressHexSync = false;

            _isLoaded = true;

            if (!string.IsNullOrEmpty(initialHexColor))
                SetFromHex(initialHexColor);
            else
                UpdateAll();
        }

        // ══════════════════════════════════════════════════════════════
        //  Canvas size changed → re-render wheel + reposition crosshair
        // ══════════════════════════════════════════════════════════════

        private void WheelCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isLoaded) return;
            RenderColorWheel();
            PositionCrosshair();
        }

        // ══════════════════════════════════════════════════════════════
        //  Color Wheel rendering
        // ══════════════════════════════════════════════════════════════

        private void RenderColorWheel()
        {
            int w = (int)WheelCanvas.ActualWidth;
            int h = (int)WheelCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double cx     = w / 2.0;
            double cy     = h / 2.0;
            double radius = Math.Min(cx, cy) - 4;

            var wb     = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double dx   = x - cx;
                    double dy   = y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    int    idx  = (y * w + x) * 4;

                    if (dist <= radius)
                    {
                        double hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360) % 360;
                        double sat = dist / radius;
                        HsvToRgb(hue, sat, 1.0, out byte r, out byte g, out byte b);
                        pixels[idx]     = b;
                        pixels[idx + 1] = g;
                        pixels[idx + 2] = r;
                        pixels[idx + 3] = 255;
                    }
                    // else transparent
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            WheelImage.Source = wb;
            WheelImage.Width  = w;
            WheelImage.Height = h;
            Canvas.SetLeft(WheelImage, 0);
            Canvas.SetTop(WheelImage,  0);
        }

        // ══════════════════════════════════════════════════════════════
        //  Crosshair positioning
        // ══════════════════════════════════════════════════════════════

        private void PositionCrosshair()
        {
            double radius = WheelRadius;
            double angle  = _hue * Math.PI / 180.0;
            double dist   = _saturation * radius;
            double cx     = WheelCenterX + Math.Cos(angle) * dist;
            double cy     = WheelCenterY + Math.Sin(angle) * dist;

            Canvas.SetLeft(CrosshairOuter, cx - 8);
            Canvas.SetTop(CrosshairOuter,  cy - 8);
            Canvas.SetLeft(CrosshairInner, cx - 3);
            Canvas.SetTop(CrosshairInner,  cy - 3);
        }

        // ══════════════════════════════════════════════════════════════
        //  Mouse interaction on wheel
        // ══════════════════════════════════════════════════════════════

        private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingWheel = true;
            WheelCanvas.CaptureMouse();
            PickFromWheelPoint(e.GetPosition(WheelCanvas));
        }

        private void Wheel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingWheel && e.LeftButton == MouseButtonState.Pressed)
                PickFromWheelPoint(e.GetPosition(WheelCanvas));
        }

        private void Wheel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingWheel = false;
            WheelCanvas.ReleaseMouseCapture();
        }

        private void PickFromWheelPoint(System.Windows.Point p)
        {
            double dx     = p.X - WheelCenterX;
            double dy     = p.Y - WheelCenterY;
            double dist   = Math.Sqrt(dx * dx + dy * dy);
            double radius = WheelRadius;

            _hue        = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360) % 360;
            _saturation = Math.Min(dist / radius, 1.0);

            PositionCrosshair();
            UpdateBrightnessGradient();
            UpdateAll();
        }

        // ══════════════════════════════════════════════════════════════
        //  Brightness slider
        // ══════════════════════════════════════════════════════════════

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            _brightness = BrightnessSlider.Value;
            UpdateAll();
        }

        private void UpdateBrightnessGradient()
        {
            HsvToRgb(_hue, _saturation, 1.0, out byte r, out byte g, out byte b);
            var hueColor = System.Windows.Media.Color.FromRgb(r, g, b);
            BrightnessTrack.Background = new LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(0, 0, 0),
                hueColor,
                new System.Windows.Point(0, 0.5),
                new System.Windows.Point(1, 0.5));
        }

        // ══════════════════════════════════════════════════════════════
        //  Hex input
        // ══════════════════════════════════════════════════════════════

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || _suppressHexSync) return;
            string text = HexTextBox.Text.TrimStart('#');
            if (text.Length == 6)
                SetFromHex("#" + text, skipHexBox: true);
        }

        private void HexTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = HexTextBox.Text.TrimStart('#');
                if (text.Length == 6)
                    SetFromHex("#" + text, skipHexBox: true);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Update UI helpers
        // ══════════════════════════════════════════════════════════════

        private void UpdateAll()
        {
            if (!_isLoaded) return;

            HsvToRgb(_hue, _saturation, _brightness, out byte r, out byte g, out byte b);
            var color = System.Windows.Media.Color.FromRgb(r, g, b);

            PreviewSwatch.Background = new SolidColorBrush(color);

            _suppressHexSync = true;
            HexTextBox.Text  = $"{r:X2}{g:X2}{b:X2}";
            _suppressHexSync = false;

            SelectedHexColor = $"#{r:X2}{g:X2}{b:X2}";
        }

        private void SetFromHex(string hex, bool skipHexBox = false)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                RgbToHsv(color.R, color.G, color.B, out double h, out double s, out double v);
                _hue        = h;
                _saturation = s;
                _brightness = v;

                PositionCrosshair();

                _suppressHexSync = true;
                BrightnessSlider.Value = _brightness;
                _suppressHexSync = false;

                if (!skipHexBox)
                {
                    _suppressHexSync = true;
                    HexTextBox.Text  = hex.TrimStart('#');
                    _suppressHexSync = false;
                }

                UpdateBrightnessGradient();
                UpdateAll();
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        //  Button handlers
        // ══════════════════════════════════════════════════════════════

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ══════════════════════════════════════════════════════════════
        //  Color math: HSV ↔ RGB
        // ══════════════════════════════════════════════════════════════

        private static void HsvToRgb(double h, double s, double v,
                                      out byte r, out byte g, out byte b)
        {
            h = h % 360;
            double c  = v * s;
            double x  = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m  = v - c;

            double r1, g1, b1;
            if      (h < 60)  { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else              { r1 = c; g1 = 0; b1 = x; }

            r = (byte)Math.Round((r1 + m) * 255);
            g = (byte)Math.Round((g1 + m) * 255);
            b = (byte)Math.Round((b1 + m) * 255);
        }

        private static void RgbToHsv(byte r, byte g, byte b,
                                      out double h, out double s, out double v)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max   = Math.Max(rf, Math.Max(gf, bf));
            double min   = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)      { h = 0; }
            else if (max == rf)  { h = 60 * (((gf - bf) / delta) % 6); }
            else if (max == gf)  { h = 60 * (((bf - rf) / delta) + 2); }
            else                 { h = 60 * (((rf - gf) / delta) + 4); }

            if (h < 0) h += 360;
        }
    }
}
