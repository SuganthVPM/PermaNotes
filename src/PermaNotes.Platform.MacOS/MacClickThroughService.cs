using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.MacOS
{
    /// <summary>
    /// Implements region-exempt click-through for Avalonia note windows on macOS.
    /// Intercepts -[NSView hitTest:] on the NSWindow's contentView using Objective-C method swizzling.
    /// If click-through is enabled for the note window:
    ///   - Clicks in the header region (top 34px) are passed to the original hitTest implementation
    ///     so the window remains draggable and title/buttons stay interactive.
    ///   - Clicks in the body area return nil (IntPtr.Zero), allowing cursor events to fall through
    ///     to the underlying desktop or other applications.
    /// </summary>
    public class MacClickThroughService : IClickThroughService
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr HitTestDelegate(IntPtr self, IntPtr cmd, CGPoint point);

        private record WindowClickState(Window Window, bool Enabled, double ExemptHeight);

        private static readonly ConcurrentDictionary<IntPtr, WindowClickState> _windowStates = new();
        private static readonly object _swizzleLock = new();
        private static bool _isSwizzled = false;

        private static readonly IntPtr SelContentView = MacNativeMethods.sel_registerName("contentView");
        private static readonly IntPtr SelWindow = MacNativeMethods.sel_registerName("window");
        private static readonly IntPtr SelHitTest = MacNativeMethods.sel_registerName("hitTest:");
        private static readonly IntPtr SelIsFlipped = MacNativeMethods.sel_registerName("isFlipped");

        private static HitTestDelegate? _originalHitTest;
        private static readonly HitTestDelegate _customHitTestDelegate = SwizzledHitTest;

        public void SetClickThrough(object window, bool enabled, (double X, double Y, double W, double H) exemptRegion)
        {
            if (window is not Window avWindow) return;

            var nsWindow = TryGetNSWindow(avWindow);
            if (nsWindow == IntPtr.Zero) return;

            // Ensure swizzling is applied once to the contentView's class
            EnsureSwizzled(nsWindow);

            if (enabled)
            {
                _windowStates[nsWindow] = new WindowClickState(avWindow, true, exemptRegion.H);
            }
            else
            {
                _windowStates.TryRemove(nsWindow, out _);
            }
        }

        private static void EnsureSwizzled(IntPtr nsWindow)
        {
            if (_isSwizzled) return;

            lock (_swizzleLock)
            {
                if (_isSwizzled) return;

                try
                {
                    var contentView = MacNativeMethods.objc_msgSend(nsWindow, SelContentView);
                    if (contentView == IntPtr.Zero) return;

                    var cls = MacNativeMethods.object_getClass(contentView);
                    if (cls == IntPtr.Zero) return;

                    var method = MacNativeMethods.class_getInstanceMethod(cls, SelHitTest);
                    if (method == IntPtr.Zero) return;

                    var originalImp = MacNativeMethods.method_getImplementation(method);
                    if (originalImp == IntPtr.Zero) return;

                    _originalHitTest = Marshal.GetDelegateForFunctionPointer<HitTestDelegate>(originalImp);

                    IntPtr customImp = Marshal.GetFunctionPointerForDelegate(_customHitTestDelegate);
                    MacNativeMethods.class_replaceMethod(cls, SelHitTest, customImp, "@@:{CGPoint=dd}");

                    _isSwizzled = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MacClickThroughService] Swizzling failed: {ex.Message}");
                }
            }
        }

        private static IntPtr SwizzledHitTest(IntPtr self, IntPtr cmd, CGPoint point)
        {
            try
            {
                IntPtr win = MacNativeMethods.objc_msgSend(self, SelWindow);
                if (win != IntPtr.Zero && _windowStates.TryGetValue(win, out var state) && state.Enabled)
                {
                    bool isFlipped = MacNativeMethods.objc_msgSend_bool(self, SelIsFlipped);
                    bool inHeader;

                    if (isFlipped)
                    {
                        // Flipped view: origin (0, 0) is top-left, Y increases downwards
                        inHeader = point.Y <= state.ExemptHeight;
                    }
                    else
                    {
                        // Standard Cocoa view: origin (0, 0) is bottom-left, Y increases upwards
                        double height = state.Window.Bounds.Height > 0 
                            ? state.Window.Bounds.Height 
                            : (state.Window.Height > 0 ? state.Window.Height : 280.0);
                        inHeader = point.Y >= (height - state.ExemptHeight);
                    }

                    if (inHeader)
                    {
                        // Header region: forward to original hitTest so buttons and drag handle remain interactive
                        return _originalHitTest != null ? _originalHitTest(self, cmd, point) : self;
                    }

                    // Body region: return nil so mouse events fall through
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacClickThroughService] HitTest exception: {ex.Message}");
            }

            // Normal hit-testing for non-clickthrough windows or fallback
            return _originalHitTest != null ? _originalHitTest(self, cmd, point) : self;
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
