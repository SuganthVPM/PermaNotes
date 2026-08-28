using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.Windows
{
    /// <summary>
    /// Pins note windows behind desktop icons using the well-known WorkerW trick:
    ///  1. Send 0x052C to Progman to spawn a WorkerW sibling.
    ///  2. Enumerate top-level windows to find the WorkerW that sits directly
    ///     behind the desktop ListView (SHELLDLL_DefView).
    ///  3. SetParent the note HWND into that WorkerW so it lives on the desktop layer.
    ///
    /// Detach restores the original parent (the desktop root), moving the window
    /// back to a normal Z-order.
    ///
    /// NOTE: Desktop pinning forces the window to sit below all normal windows.
    ///       It is mutually exclusive with AlwaysOnTop; the caller (NoteWindow.axaml.cs)
    ///       must clear Topmost before calling Attach.
    /// </summary>
    public class WindowsDesktopPinService : IDesktopPinService
    {
        private IntPtr _workerW = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter,
            string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hwnd, int nIndex);

        public static IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong)
        {
            if (Environment.Is64BitProcess)
                return SetWindowLongPtr64(hwnd, nIndex, dwNewLong);
            return SetWindowLongPtr32(hwnd, nIndex, dwNewLong);
        }

        public static IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex)
        {
            if (Environment.Is64BitProcess)
                return GetWindowLongPtr64(hwnd, nIndex);
            return GetWindowLongPtr32(hwnd, nIndex);
        }

        private const int GWLP_HWNDPARENT = -8;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        private const uint WM_SPAWN_WORKER = 0x052C;

        /// <summary>Attempts to locate the WorkerW layer. Returns true if found.</summary>
        public bool Initialize()
        {
            _workerW = FindWorkerW();
            return _workerW != IntPtr.Zero;
        }

        /// <summary>
        /// Re-runs the WorkerW search. Useful after explorer.exe restarts (e.g. sign-in after lock).
        /// </summary>
        public bool Reinitialize() => Initialize();

        /// <summary>Reparents an Avalonia window into the WorkerW desktop layer.</summary>
        public bool Attach(object window)
        {
            if (window is not Window avWindow) return false;
            if (_workerW == IntPtr.Zero && !Initialize()) return false;

            var hwnd = TryGetHwnd(avWindow);
            if (hwnd == IntPtr.Zero) return false;

            // Set OWNER to WorkerW (not PARENT)
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, _workerW);

            // Add WS_EX_TOOLWINDOW: prevents taskbar entry and Alt+Tab
            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long style = exStyle.ToInt64();
            style |= WS_EX_TOOLWINDOW;
            style &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));

            return true;
        }

        /// <summary>Removes a note window from the desktop layer, restoring normal stacking.</summary>
        public void Detach(object window)
        {
            if (window is not Window avWindow) return;

            var hwnd = TryGetHwnd(avWindow);
            if (hwnd == IntPtr.Zero) return;

            // Remove owner
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

            // Restore normal style
            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long style = exStyle.ToInt64();
            style &= ~WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        }

        // ── WorkerW discovery ──────────────────────────────────────────────────

        private IntPtr FindWorkerW()
        {
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            // Instruct Progman to spawn the WorkerW sibling
            SendMessage(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero);

            IntPtr workerW = IntPtr.Zero;

            EnumWindows((hwnd, _) =>
            {
                // We're looking for a WorkerW that has a SHELLDLL_DefView child
                var shellDll = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDll != IntPtr.Zero)
                {
                    // The WorkerW we want is the NEXT sibling of the one containing SHELLDLL_DefView
                    workerW = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    return false; // stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            return workerW;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static IntPtr TryGetHwnd(Window w)
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
