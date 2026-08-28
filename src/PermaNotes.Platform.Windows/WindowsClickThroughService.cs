using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.Windows
{
    /// <summary>
    /// Implements click-through for a note window using WM_NCHITTEST interception via a Win32 sub-class hook.
    /// The header region (Y < exemptHeight) forwards normal hit-testing so the drag bar and header buttons
    /// remain fully clickable. The body area returns HTTRANSPARENT so mouse events fall through to whatever
    /// is behind the window.
    /// </summary>
    public class WindowsClickThroughService : IClickThroughService
    {
        // Win32 constants
        private const int GWL_EXSTYLE    = -20;
        private const int WS_EX_LAYERED  = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private const int WM_NCHITTEST   = 0x0084;
        private const int HTTRANSPARENT  = -1;
        private const int HTCLIENT       = 1;

        // SetWindowSubclass / WNDPROC sub-classing
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        private const int GWLP_WNDPROC = -4;

        // Per-HWND state so multiple windows can each have independent sub-classes
        private record SubClassState(
            WndProcDelegate Proc,
            IntPtr OldProc,
            double ExemptHeight,   // pixels from top that stay clickable (header)
            double ExemptWidth     // full width — not trimmed, but stored for future use
        );

        private readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, SubClassState> _states = new();

        /// <summary>
        /// Enable or disable click-through on an Avalonia <see cref="Window"/>.
        /// </summary>
        /// <param name="window">Must be an Avalonia <see cref="Window"/> instance.</param>
        /// <param name="enabled">True to enable click-through, false to restore normal behaviour.</param>
        /// <param name="exemptRegion">
        /// (X, Y, W, H) of the region that stays clickable even when click-through is on.
        /// Typically the header drag bar — pass (0, 0, fullWidth, headerHeight).
        /// </param>
        public void SetClickThrough(object window, bool enabled,
            (double X, double Y, double W, double H) exemptRegion)
        {
            if (window is not Window avWindow) return;

            var hwnd = TryGetHwnd(avWindow);
            if (hwnd == IntPtr.Zero) return;

            if (enabled)
            {
                EnableClickThrough(hwnd, exemptRegion);
            }
            else
            {
                DisableClickThrough(hwnd);
            }
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static IntPtr TryGetHwnd(Window w)
        {
            try
            {
                var platformImpl = w.TryGetPlatformHandle();
                return platformImpl?.Handle ?? IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private void EnableClickThrough(IntPtr hwnd, (double X, double Y, double W, double H) exempt)
        {
            // Add WS_EX_LAYERED | WS_EX_TRANSPARENT style so the OS knows the window can be transparent
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);

            if (_states.ContainsKey(hwnd)) return; // already sub-classed

            // Sub-class the window so WM_NCHITTEST can selectively return HTCLIENT vs HTTRANSPARENT
            WndProcDelegate proc = (h, msg, w, l) => SubClassWndProc(h, msg, w, l);

            IntPtr oldProc = SetWndProc(hwnd, Marshal.GetFunctionPointerForDelegate(proc));

            _states[hwnd] = new SubClassState(proc, oldProc, exempt.H, exempt.W);
        }

        private void DisableClickThrough(IntPtr hwnd)
        {
            if (_states.TryRemove(hwnd, out var state))
            {
                SetWndProc(hwnd, state.OldProc);
            }

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED & ~WS_EX_TRANSPARENT);
        }

        private IntPtr SubClassWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCHITTEST && _states.TryGetValue(hwnd, out var state))
            {
                // Decode cursor position (low word = X, high word = Y — screen coords)
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                // Get the window rect to convert screen → client coords
                if (NativeMethods.GetWindowRect(hwnd, out var rect))
                {
                    double clientY = y - rect.Top;

                    if (clientY <= state.ExemptHeight)
                    {
                        // Header area — let Windows do normal hit-testing (draggable)
                        return CallWindowProc(state.OldProc, hwnd, msg, wParam, lParam);
                    }
                }

                // Body area — transparent to clicks
                return new IntPtr(HTTRANSPARENT);
            }

            if (_states.TryGetValue(hwnd, out var s))
                return CallWindowProc(s.OldProc, hwnd, msg, wParam, lParam);

            return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private static IntPtr SetWndProc(IntPtr hwnd, IntPtr newProc)
        {
            return Environment.Is64BitProcess
                ? SetWindowLongPtr64(hwnd, GWLP_WNDPROC, newProc)
                : SetWindowLongPtr32(hwnd, GWLP_WNDPROC, newProc);
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

            [DllImport("user32.dll")]
            internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            internal struct RECT
            {
                public int Left, Top, Right, Bottom;
            }
        }
    }
}
