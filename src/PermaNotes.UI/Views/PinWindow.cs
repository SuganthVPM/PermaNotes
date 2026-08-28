using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PermaNotes.UI.Views
{
    public class PinWindow : Window
    {
        private readonly Action _onClick;

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hwnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public PinWindow(Action onClick)
        {
            _onClick = onClick;
            
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = new Cursor(StandardCursorType.Hand);
            
            // Completely transparent hit-testable border that lets the real button show through
            // with zero visible artifacts or dimension mismatches
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            Content = border;

            ToolTip.SetTip(this, "Disable Click-Through");

            // Exclude from Alt+Tab on Windows by adding WS_EX_TOOLWINDOW
            Opened += (s, e) =>
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    try
                    {
                        var handle = TryGetPlatformHandle();
                        if (handle != null && handle.Handle != IntPtr.Zero)
                        {
                            IntPtr hwnd = handle.Handle;
                            int exStyle = GetWindowLong32(hwnd, GWL_EXSTYLE);
                            SetWindowLong32(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                        }
                    }
                    catch { }
                }
            };
            
            PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    _onClick?.Invoke();
                }
            };
        }
    }
}
