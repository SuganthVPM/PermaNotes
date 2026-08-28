using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using PermaNotes.Core.Services;

namespace PermaNotes.Platform.Windows
{
    /// <summary>
    /// Manages desktop shell integration using the Progman/WorkerW technique.
    /// Uses GWLP_HWNDPARENT (owner) approach to preserve Avalonia's rendering pipeline while
    /// making notes follow the desktop's show/hide behavior (surviving Win+D) and
    /// removing them from Alt+Tab / Taskbar via WS_EX_TOOLWINDOW.
    /// </summary>
    public class WindowsDesktopPinService : IDesktopPinService
    {
        private IntPtr _desktopHost = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong)
        {
            if (Environment.Is64BitProcess)
                return SetWindowLongPtr64(hwnd, nIndex, dwNewLong);
            return SetWindowLongPtr32(hwnd, nIndex, dwNewLong);
        }

        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex)
        {
            if (Environment.Is64BitProcess)
                return GetWindowLongPtr64(hwnd, nIndex);
            return GetWindowLongPtr32(hwnd, nIndex);
        }

        private const int GWLP_HWNDPARENT = -8;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint WM_SPAWN_WORKER = 0x052C;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        public bool Initialize()
        {
            _desktopHost = FindDesktopHost();
            return _desktopHost != IntPtr.Zero;
        }

        public bool Reinitialize() => Initialize();

        public bool Attach(object window)
        {
            if (window is not Window avWindow) return false;
            if (_desktopHost == IntPtr.Zero && !Initialize()) return false;

            var hwnd = TryGetHwnd(avWindow);
            if (hwnd == IntPtr.Zero) return false;

            try
            {
                // Set OWNER to Progman / WorkerW (not PARENT - preserves coordinates and rendering)
                // This makes Win+D show/hide the note along with the desktop
                SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, _desktopHost);

                // Apply WS_EX_TOOLWINDOW and remove WS_EX_APPWINDOW: prevents Alt+Tab and taskbar presence
                IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
                long style = exStyle.ToInt64();
                style |= WS_EX_TOOLWINDOW;
                style &= ~WS_EX_APPWINDOW;
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));

                // Notify Windows shell of the style changes
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Detach(object window)
        {
            if (window is not Window avWindow) return;

            var hwnd = TryGetHwnd(avWindow);
            if (hwnd == IntPtr.Zero) return;

            try
            {
                // Remove owner relationship
                SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

                // Restore normal window style (remove TOOLWINDOW)
                IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
                long style = exStyle.ToInt64();
                style &= ~WS_EX_TOOLWINDOW;
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));

                // Notify Windows shell of the style changes
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch
            {
            }
        }

        private IntPtr FindDesktopHost()
        {
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            // Send message 0x052C to Progman to spawn WorkerW if not already present
            SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            IntPtr workerW = IntPtr.Zero;

            EnumWindows((hwnd, _) =>
            {
                var shellDll = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDll != IntPtr.Zero)
                {
                    // The WorkerW we want is the sibling after the window containing SHELLDLL_DefView
                    workerW = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    return false; // stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            // Fallback: If no WorkerW sibling was found (common on Windows 11), use Progman itself!
            if (workerW == IntPtr.Zero)
            {
                workerW = progman;
            }

            return workerW;
        }

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
