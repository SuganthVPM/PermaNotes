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

        public PinWindow(Action onClick, IBrush? iconColor = null)
        {
            _onClick = onClick;
            
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = new Cursor(StandardCursorType.Hand);
            
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse("#402196F3")),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M21,3H7C5.89,3,5,3.89,5,5v4H3v12c0,1.1,0.89,2,2,2h12v-2H5V11h14v2h2V5C21,3.89,20.1,3,21,3z M19,9H7V5h12V9z M14,14v8l2.25-2.25l2.5,4.25l1.5-0.75l-2.5-4.25l3.5,0L14,14z"),
                Fill = iconColor ?? Brushes.Black,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            border.Child = path;
            Content = border;

            ToolTip.SetTip(this, "Disable Click-Through");

            border.PointerEntered += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.Parse("#602196F3"));
            };

            border.PointerExited += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.Parse("#402196F3"));
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
