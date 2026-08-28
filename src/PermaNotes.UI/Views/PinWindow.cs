using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace PermaNotes.UI.Views
{
    public class PinWindow : Window
    {
        private Action _onClick;

        public PinWindow(Action onClick)
        {
            _onClick = onClick;
            
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            
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
