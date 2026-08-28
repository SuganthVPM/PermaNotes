using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PermaNotes.UI.Views
{
    public partial class ColorWheelDialog : Window
    {
        public string? SelectedHexColor { get; private set; }

        public ColorWheelDialog()
        {
            InitializeComponent();
        }

        public ColorWheelDialog(string? initialHexColor) : this()
        {
            if (!string.IsNullOrWhiteSpace(initialHexColor))
            {
                try
                {
                    MainColorView.Color = Color.Parse(initialHexColor);
                }
                catch { }
            }
        }

        private void ApplyButton_Click(object? sender, RoutedEventArgs e)
        {
            var color = MainColorView.Color;
            SelectedHexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            Close(SelectedHexColor);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
