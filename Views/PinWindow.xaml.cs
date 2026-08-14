using System;
using System.Windows;
using System.Windows.Input;

namespace DesktopNotes.Views
{
    public partial class PinWindow : Window
    {
        private readonly Action _onPinClicked;

        public PinWindow(Action onPinClicked)
        {
            InitializeComponent();
            _onPinClicked = onPinClicked;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _onPinClicked?.Invoke();
        }
    }
}
