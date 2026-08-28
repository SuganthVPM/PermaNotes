using System;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaNotes.Core.Models;

namespace PermaNotes.UI.ViewModels
{
    public partial class NoteViewModel : ObservableObject
    {
        public Note Model { get; }
        private readonly Action? _onModified;

        public static readonly Dictionary<string, string> BorderColorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "#FFF9C4", "#D0C070" }, // Yellow
            { "#FFE0B2", "#DCA457" }, // Orange
            { "#90CAF9", "#5B9BD5" }, // Blue
            { "#80CBC4", "#529B94" }, // Teal
            { "#A5D6A7", "#6DAF72" }, // Green
            { "#CE93D8", "#A163AB" }, // Purple
            { "#F48FB1", "#D46A8A" }, // Pink
            { "#FAFAFA", "#C0C0C0" }, // White
            { "#E0E0E0", "#A0A0A0" }, // Gray
        };

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private string _rtfText;

        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        [ObservableProperty]
        private string _backgroundColor;

        [ObservableProperty]
        private double _opacity;

        [ObservableProperty]
        private bool _isAlwaysOnTop;

        [ObservableProperty]
        private bool _isClosed;

        [ObservableProperty]
        private bool _isLocked;

        [ObservableProperty]
        private bool _isClickThrough;

        [ObservableProperty]
        private bool _isGlassmorphic;

        public bool IsMacOS => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

        [ObservableProperty]
        private bool _isEditingTitle;

        [ObservableProperty]
        private DateTime _updatedAt;

        public Guid Id => Model.Id;
        public DateTime CreatedAt => Model.CreatedAt;

        // Visual computed properties
        public IBrush CardBackground => GetCardBackground();
        public IBrush CardBorderBrush => GetCardBorderBrush();
        public double BorderThickness => IsGlassmorphic ? 1.0 : 0.0;
        public IBrush TextColor => GetAdaptiveTextColor();
        public IBrush HeaderColor => GetAdaptiveHeaderColor();
        public IBrush IconColor => GetAdaptiveIconColor();

        // Note Manager status helpers
        public string StatusText => IsClosed ? "Closed" : "Open";
        public string ToggleButtonText => IsClosed ? "Open" : "Close";
        public IBrush StatusBadgeBackground => IsClosed 
            ? new SolidColorBrush(Color.Parse("#25888888")) 
            : new SolidColorBrush(Color.Parse("#252E7D32"));
        public IBrush StatusBadgeForeground => IsClosed 
            ? new SolidColorBrush(Color.Parse("#888888")) 
            : new SolidColorBrush(Color.Parse("#2E7D32"));
        public string TopmostIndicator => IsAlwaysOnTop ? "📌 Always on Top" : "";

        public NoteViewModel(Note note, Action? onModified = null)
        {
            Model = note;
            _onModified = onModified;

            _title = note.Title;
            _text = note.Text;
            _rtfText = note.RtfText;
            _x = note.X;
            _y = note.Y;
            _width = note.Width;
            _height = note.Height;
            _backgroundColor = string.IsNullOrWhiteSpace(note.BackgroundColor) ? "#FFF9C4" : note.BackgroundColor;
            _opacity = note.Opacity <= 0 ? 1.0 : note.Opacity;
            _isAlwaysOnTop = note.IsAlwaysOnTop;
            _isClosed = note.IsClosed;
            _isLocked = note.IsLocked;
            _isClickThrough = note.IsClickThrough;
            _isGlassmorphic = note.IsGlassmorphic;
            _updatedAt = note.UpdatedAt;
        }

        partial void OnIsGlassmorphicChanged(bool value)
        {
            Model.IsGlassmorphic = value;
            NotifyVisualChanges();
            NotifyChange();
        }

        partial void OnTitleChanged(string value)
        {
            Model.Title = value;
            NotifyChange();
        }

        partial void OnTextChanged(string value)
        {
            Model.Text = value;
            NotifyChange();
        }

        partial void OnRtfTextChanged(string value)
        {
            Model.RtfText = value;
            NotifyChange();
        }

        partial void OnXChanged(double value)
        {
            Model.X = value;
            NotifyChange();
        }

        partial void OnYChanged(double value)
        {
            Model.Y = value;
            NotifyChange();
        }

        partial void OnWidthChanged(double value)
        {
            Model.Width = value;
            NotifyChange();
        }

        partial void OnHeightChanged(double value)
        {
            Model.Height = value;
            NotifyChange();
        }

        partial void OnBackgroundColorChanged(string value)
        {
            Model.BackgroundColor = value;
            NotifyVisualChanges();
            NotifyChange();
        }

        partial void OnOpacityChanged(double value)
        {
            Model.Opacity = value;
            NotifyVisualChanges();
            NotifyChange();
        }

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
            Model.IsAlwaysOnTop = value;
            OnPropertyChanged(nameof(TopmostIndicator));
            NotifyChange();
        }

        partial void OnIsClosedChanged(bool value)
        {
            Model.IsClosed = value;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ToggleButtonText));
            OnPropertyChanged(nameof(StatusBadgeBackground));
            OnPropertyChanged(nameof(StatusBadgeForeground));
            NotifyChange();
        }

        partial void OnIsLockedChanged(bool value)
        {
            Model.IsLocked = value;
            OnPropertyChanged(nameof(LockIconData));
            OnPropertyChanged(nameof(LockToolTip));
            NotifyChange();
        }

        partial void OnIsClickThroughChanged(bool value)
        {
            Model.IsClickThrough = value;
            if (value)
            {
                IsAlwaysOnTop = true;
            }
            NotifyChange();
        }

        public string LockToolTip => IsLocked ? "Unlock Note" : "Lock Note";

        public string LockIconData => IsLocked
            ? "M18,8H17V6C17,3.24 14.76,1 12,1C9.24,1 7,3.24 7,6V8H6C4.9,8 4,8.9 4,10V20C4,21.1 4.9,22 6,22H18C19.1,22 20,21.1 20,20V10C20,8.9 19.1,8 18,8M12,17C10.9,17 10,16.1 10,15C10,13.9 10.9,13 12,13C13.1,13 14,13.9 14,15C14,16.1 13.1,17 12,17M15.1,8H8.9V6C8.9,4.29 10.29,2.9 12,2.9C13.71,2.9 15.1,4.29 15.1,6V8Z"
            : "M18,8H17V6C17,3.24 14.76,1 12,1C9.24,1 7,3.24 7,6H8.9C8.9,4.29 10.29,2.9 12,2.9C13.71,2.9 15.1,4.29 15.1,6V8H6C4.9,8 4,8.9 4,10V20C4,21.1 4.9,22 6,22H18C19.1,22 20,21.1 20,20V10C20,8.9 19.1,8 18,8M18,20H6V10H18V20M12,17C10.9,17 10,16.1 10,15C10,13.9 10.9,13 12,13C13.1,13 14,13.9 14,15C14,16.1 13.1,17 12,17Z";

        public void NotifyChange()
        {
            UpdatedAt = DateTime.Now;
            Model.UpdatedAt = UpdatedAt;
            _onModified?.Invoke();
        }

        private void NotifyVisualChanges()
        {
            OnPropertyChanged(nameof(CardBackground));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(BorderThickness));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(LockIconData));
            OnPropertyChanged(nameof(HeaderColor));
            OnPropertyChanged(nameof(IconColor));
        }

        private Color GetParsedColor()
        {
            try
            {
                return Color.Parse(BackgroundColor);
            }
            catch
            {
                return Color.FromRgb(0xFF, 0xF9, 0xC4);
            }
        }

        private IBrush GetCardBackground()
        {
            var color = GetParsedColor();
            if (IsGlassmorphic)
            {
                // Apple glassmorphic translucent tint (translucent card letting behind-window blur shine through)
                byte glassAlpha = (byte)Math.Clamp((int)(Opacity * 65), 12, 115);
                return new SolidColorBrush(Color.FromArgb(glassAlpha, color.R, color.G, color.B));
            }

            byte alpha = (byte)Math.Clamp((int)(Opacity * 255), 20, 255);
            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }

        private IBrush GetCardBorderBrush()
        {
            if (IsGlassmorphic)
            {
                // Apple frosted glass white sheen border (1px crisp translucent border)
                return new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            }

            var color = GetParsedColor();
            byte alpha = (byte)Math.Clamp((int)(Opacity * 255), 20, 255);

            if (BorderColorMap.TryGetValue(BackgroundColor, out var borderHex))
            {
                try
                {
                    var bColor = Color.Parse(borderHex);
                    return new SolidColorBrush(Color.FromArgb(alpha, bColor.R, bColor.G, bColor.B));
                }
                catch { }
            }

            // Darken base color by 30%
            return new SolidColorBrush(Color.FromArgb(
                alpha,
                (byte)(color.R * 0.7),
                (byte)(color.G * 0.7),
                (byte)(color.B * 0.7)));
        }

        private bool IsDark()
        {
            var color = GetParsedColor();
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return luminance < 0.5;
        }

        private IBrush GetAdaptiveTextColor()
        {
            return IsDark() ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        }

        private IBrush GetAdaptiveHeaderColor()
        {
            return IsDark() ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        }

        private IBrush GetAdaptiveIconColor()
        {
            return IsDark() ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        }

        [RelayCommand]
        public void ToggleGlassmorphism()
        {
            IsGlassmorphic = !IsGlassmorphic;
        }

        [RelayCommand]
        public void SetGlass()
        {
            IsGlassmorphic = true;
            BackgroundColor = "#FAFAFA";
        }

        [RelayCommand]
        public void ToggleLock()
        {
            IsLocked = !IsLocked;
        }

        [RelayCommand]
        public void ToggleAlwaysOnTop()
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
        }

        [RelayCommand]
        public void ToggleClickThrough()
        {
            IsClickThrough = !IsClickThrough;
        }

        [RelayCommand]
        public void SetColor(string hex)
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                BackgroundColor = hex;
            }
        }

        [RelayCommand]
        public void SetOpacity(string opacityStr)
        {
            if (double.TryParse(opacityStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                Opacity = Math.Clamp(val, 0.1, 1.0);
            }
        }
    }
}
