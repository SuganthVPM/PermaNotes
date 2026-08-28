using System;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.MacOS
{
    /// <summary>
    /// Pins note windows behind desktop icons on macOS using native NSWindow APIs:
    ///  1. Sets window.level to kCGDesktopWindowLevel (-1000 or dynamically queried via CGWindowLevelForKey).
    ///  2. Sets collectionBehavior to NSWindowCollectionBehaviorCanJoinAllSpaces |
    ///     NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle (81).
    ///     This ensures the note window persists across all Spaces/Desktops and doesn't get
    ///     pushed out during Mission Control / F11 Show Desktop.
    ///  3. Calls orderBack: to place it behind standard desktop elements.
    /// </summary>
    public class MacDesktopPinService : IDesktopPinService
    {
        private static readonly IntPtr SelSetLevel = MacNativeMethods.sel_registerName("setLevel:");
        private static readonly IntPtr SelSetCollectionBehavior = MacNativeMethods.sel_registerName("setCollectionBehavior:");
        private static readonly IntPtr SelOrderBack = MacNativeMethods.sel_registerName("orderBack:");
        private static readonly IntPtr SelOrderFront = MacNativeMethods.sel_registerName("orderFront:");

        public bool Initialize() => true;

        public bool Reinitialize() => true;

        public bool Attach(object window)
        {
            if (window is not Window avWindow) return false;

            var nsWindow = TryGetNSWindow(avWindow);
            if (nsWindow == IntPtr.Zero) return false;

            try
            {
                int desktopLevel = MacNativeMethods.GetDesktopWindowLevel();
                ulong behavior = MacNativeMethods.NSWindowCollectionBehaviorCanJoinAllSpaces
                               | MacNativeMethods.NSWindowCollectionBehaviorStationary
                               | MacNativeMethods.NSWindowCollectionBehaviorIgnoresCycle; // 81

                MacNativeMethods.objc_msgSend(nsWindow, SelSetLevel, desktopLevel);
                MacNativeMethods.objc_msgSend(nsWindow, SelSetCollectionBehavior, behavior);
                MacNativeMethods.objc_msgSend(nsWindow, SelOrderBack, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacDesktopPinService] Attach failed: {ex.Message}");
                return false;
            }
        }

        public void Detach(object window)
        {
            if (window is not Window avWindow) return;

            var nsWindow = TryGetNSWindow(avWindow);
            if (nsWindow == IntPtr.Zero) return;

            try
            {
                int normalLevel = MacNativeMethods.GetNormalWindowLevel();
                ulong defaultBehavior = MacNativeMethods.NSWindowCollectionBehaviorDefault;

                MacNativeMethods.objc_msgSend(nsWindow, SelSetLevel, normalLevel);
                MacNativeMethods.objc_msgSend(nsWindow, SelSetCollectionBehavior, defaultBehavior);
                MacNativeMethods.objc_msgSend(nsWindow, SelOrderFront, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacDesktopPinService] Detach failed: {ex.Message}");
            }
        }

        private static IntPtr TryGetNSWindow(Window w)
        {
            try
            {
                return w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}
