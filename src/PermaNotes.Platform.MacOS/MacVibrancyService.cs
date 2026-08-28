using System;
using System.Collections.Generic;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.MacOS
{
    /// <summary>
    /// Implements native Apple Glass / Vibrancy (NSVisualEffectView) behind note windows on macOS.
    /// Uses NSVisualEffectMaterialHUDWindow (13), NSVisualEffectBlendingModeBehindWindow (0),
    /// and NSVisualEffectStateActive (1) to provide hardware-accelerated frosted glassmorphism
    /// blurring the desktop and wallpaper behind the note window.
    /// </summary>
    public class MacVibrancyService : IVibrancyService
    {
        private static readonly IntPtr SelContentView = MacNativeMethods.sel_registerName("contentView");
        private static readonly IntPtr SelSetOpaque = MacNativeMethods.sel_registerName("setOpaque:");
        private static readonly IntPtr SelSetBackgroundColor = MacNativeMethods.sel_registerName("setBackgroundColor:");
        private static readonly IntPtr SelClearColor = MacNativeMethods.sel_registerName("clearColor");
        private static readonly IntPtr SelAlloc = MacNativeMethods.sel_registerName("alloc");
        private static readonly IntPtr SelInit = MacNativeMethods.sel_registerName("init");
        private static readonly IntPtr SelSetAutoresizingMask = MacNativeMethods.sel_registerName("setAutoresizingMask:");
        private static readonly IntPtr SelSetMaterial = MacNativeMethods.sel_registerName("setMaterial:");
        private static readonly IntPtr SelSetBlendingMode = MacNativeMethods.sel_registerName("setBlendingMode:");
        private static readonly IntPtr SelSetState = MacNativeMethods.sel_registerName("setState:");
        private static readonly IntPtr SelSetWantsLayer = MacNativeMethods.sel_registerName("setWantsLayer:");
        private static readonly IntPtr SelAddSubviewPositioned = MacNativeMethods.sel_registerName("addSubview:positioned:relativeTo:");
        private static readonly IntPtr SelSetHidden = MacNativeMethods.sel_registerName("setHidden:");

        // NSVisualEffectMaterialHUDWindow (13) - crisp frosted glass material in AppKit
        private const int NSVisualEffectMaterialHUDWindow = 13;

        // NSVisualEffectBlendingModeBehindWindow (0) - blurs underlying desktop & windows
        private const int NSVisualEffectBlendingModeBehindWindow = 0;

        // NSVisualEffectStateActive (1) - keeps vibrancy frosted even when unfocused
        private const int NSVisualEffectStateActive = 1;

        // NSViewWidthSizable (2) | NSViewHeightSizable (16) = 18
        private const ulong NSViewWidthAndHeightSizable = 18UL;

        // Track attached effect views per window handle
        private readonly Dictionary<IntPtr, IntPtr> _vfxViews = new();

        public bool IsSupported => true;

        public bool SetVibrancy(object window, bool enabled)
        {
            if (window is not Window avWindow) return false;

            var nsWindow = TryGetNSWindow(avWindow);
            if (nsWindow == IntPtr.Zero) return false;

            try
            {
                // Ensure window itself is non-opaque with clear background for AppKit compositing
                MacNativeMethods.objc_msgSend_setBool(nsWindow, SelSetOpaque, false);

                var nsColorClass = MacNativeMethods.objc_getClass("NSColor");
                if (nsColorClass != IntPtr.Zero)
                {
                    var clearColor = MacNativeMethods.objc_msgSend(nsColorClass, SelClearColor);
                    if (clearColor != IntPtr.Zero)
                    {
                        MacNativeMethods.objc_msgSend(nsWindow, SelSetBackgroundColor, clearColor);
                    }
                }

                var contentView = MacNativeMethods.objc_msgSend(nsWindow, SelContentView);
                if (contentView == IntPtr.Zero) return false;

                if (enabled)
                {
                    if (!_vfxViews.TryGetValue(nsWindow, out var vfxView) || vfxView == IntPtr.Zero)
                    {
                        var clsEffectView = MacNativeMethods.objc_getClass("NSVisualEffectView");
                        if (clsEffectView != IntPtr.Zero)
                        {
                            var allocated = MacNativeMethods.objc_msgSend(clsEffectView, SelAlloc);
                            vfxView = MacNativeMethods.objc_msgSend(allocated, SelInit);

                            if (vfxView != IntPtr.Zero)
                            {
                                // Autoresize with window dimensions
                                MacNativeMethods.objc_msgSend(vfxView, SelSetAutoresizingMask, NSViewWidthAndHeightSizable);

                                // Apple HUD frosted material
                                MacNativeMethods.objc_msgSend(vfxView, SelSetMaterial, NSVisualEffectMaterialHUDWindow);

                                // BehindWindow: blurs the macOS desktop behind the note window
                                MacNativeMethods.objc_msgSend(vfxView, SelSetBlendingMode, NSVisualEffectBlendingModeBehindWindow);

                                // Active: preserves frosted blur regardless of window focus
                                MacNativeMethods.objc_msgSend(vfxView, SelSetState, NSVisualEffectStateActive);

                                // CoreAnimation layer backing
                                MacNativeMethods.objc_msgSend_setBool(vfxView, SelSetWantsLayer, true);

                                // Add subview at lowest z-order (-1 = NSWindowBelow)
                                MacNativeMethods.objc_msgSend(contentView, SelAddSubviewPositioned, vfxView, (IntPtr)(-1), IntPtr.Zero);

                                _vfxViews[nsWindow] = vfxView;
                            }
                        }
                    }
                    else
                    {
                        MacNativeMethods.objc_msgSend_setBool(vfxView, SelSetHidden, false);
                    }
                }
                else
                {
                    if (_vfxViews.TryGetValue(nsWindow, out var vfxView) && vfxView != IntPtr.Zero)
                    {
                        MacNativeMethods.objc_msgSend_setBool(vfxView, SelSetHidden, true);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacVibrancyService] SetVibrancy error: {ex.Message}");
                return false;
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
