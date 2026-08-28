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
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            Content = border;

            ToolTip.SetTip(this, "Disable Click-Through");

            border.PointerEntered += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(0x25, 0, 0, 0));
            };

            border.PointerExited += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
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
