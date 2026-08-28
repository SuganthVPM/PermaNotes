using PermaNotes.Core.Services;

namespace PermaNotes.Platform.Windows
{
    /// <summary>
    /// Windows implementation of IVibrancyService.
    /// Returns false since Apple/iOS/macOS NSVisualEffectView glassmorphism is native to macOS.
    /// </summary>
    public class WindowsVibrancyService : IVibrancyService
    {
        public bool IsSupported => false;

        public bool SetVibrancy(object window, bool enabled)
        {
            return false;
        }
    }
}
