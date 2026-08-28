using System;
using System.Runtime.InteropServices;
using Avalonia;
using PermaNotes.Core.Services;
using PermaNotes.UI;

namespace PermaNotes.Desktop
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Resolve platform services before the Avalonia loop starts
            IClickThroughService? clickThrough = null;
            IDesktopPinService?   desktopPin  = null;
            IStartupService?      startup     = null;
            IVibrancyService?     vibrancy    = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                clickThrough = new PermaNotes.Platform.Windows.WindowsClickThroughService();
                desktopPin   = new PermaNotes.Platform.Windows.WindowsDesktopPinService();
                startup      = new PermaNotes.Platform.Windows.WindowsStartupService();
                vibrancy     = new PermaNotes.Platform.Windows.WindowsVibrancyService();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                clickThrough = new PermaNotes.Platform.MacOS.MacClickThroughService();
                desktopPin   = new PermaNotes.Platform.MacOS.MacDesktopPinService();
                startup      = new PermaNotes.Platform.MacOS.MacStartupService();
                vibrancy     = new PermaNotes.Platform.MacOS.MacVibrancyService();
            }

            BuildAvaloniaApp(clickThrough, desktopPin, startup, vibrancy)
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp(
            IClickThroughService? clickThrough = null,
            IDesktopPinService?   desktopPin  = null,
            IStartupService?      startup     = null,
            IVibrancyService?     vibrancy    = null)
            => AppBuilder.Configure(() => new App(clickThrough, desktopPin, startup, vibrancy))
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

