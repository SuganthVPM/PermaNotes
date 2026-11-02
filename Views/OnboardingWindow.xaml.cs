using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopNotes.Views
{
    public enum OnboardingMode
    {
        FirstTime,
        Update,
        Help
    }

    public partial class OnboardingWindow : Window
    {
        private int _currentPage = 1;
        private readonly OnboardingMode _mode;

        public OnboardingWindow(OnboardingMode mode = OnboardingMode.FirstTime)
        {
            InitializeComponent();
            _mode = mode;

            if (_mode == OnboardingMode.Update)
            {
                Title = "PermaNotes Update";
                // Optionally change text on Page1 for updates
                if (Page1.Children[0] is System.Windows.Controls.TextBlock tb)
                {
                    tb.Text = "PermaNotes has been Updated";
                    if (Page1.Children[1] is System.Windows.Controls.TextBlock tb2)
                    {
                        tb2.Text = "Check out the latest features and formatting tools below.";
                    }
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 1)
            {
                _currentPage = 2;
                Page1.Visibility = Visibility.Collapsed;
                Page2.Visibility = Visibility.Visible;
                BtnBack.Visibility = Visibility.Visible;
                UpdateDots();
            }
            else if (_currentPage == 2)
            {
                _currentPage = 3;
                Page2.Visibility = Visibility.Collapsed;
                Page3.Visibility = Visibility.Visible;
                BtnNext.Content = "Finish";
                UpdateDots();
            }
            else if (_currentPage == 3)
            {
                Close();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 2)
            {
                _currentPage = 1;
                Page2.Visibility = Visibility.Collapsed;
                Page1.Visibility = Visibility.Visible;
                BtnBack.Visibility = Visibility.Collapsed;
                UpdateDots();
            }
            else if (_currentPage == 3)
            {
                _currentPage = 2;
                Page3.Visibility = Visibility.Collapsed;
                Page2.Visibility = Visibility.Visible;
                BtnNext.Content = "Next";
                UpdateDots();
            }
        }

        private void UpdateDots()
        {
            var activeColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007ACC"));
            var inactiveColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555"));

            Dot1.Fill = _currentPage == 1 ? activeColor : inactiveColor;
            Dot2.Fill = _currentPage == 2 ? activeColor : inactiveColor;
            Dot3.Fill = _currentPage == 3 ? activeColor : inactiveColor;
        }
    }
}
