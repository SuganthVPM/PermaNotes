namespace PermaNotes.Core.Services
{
    /// <summary>
    /// Service for platform-specific window vibrancy / glassmorphism.
    /// On macOS, uses native NSVisualEffectView (behind-window blur) for hardware-accelerated Apple Glassmorphism.
    /// </summary>
    public interface IVibrancyService
    {
        /// <summary>Whether the current operating system supports native glassmorphism / vibrancy.</summary>
        bool IsSupported { get; }

        /// <summary>Enables or disables native hardware-accelerated vibrancy / glassmorphism for the window.</summary>
        bool SetVibrancy(object window, bool enabled);
    }
}
