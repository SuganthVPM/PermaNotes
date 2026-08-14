using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using DesktopNotes.Interop;

namespace DesktopNotes.Services
{
    /// <summary>
    /// Manages desktop shell integration using the Progman/WorkerW technique.
    /// Uses GWLP_HWNDPARENT (owner) approach to preserve WPF rendering while
    /// making notes follow the desktop's show/hide behavior (Win+D).
    /// </summary>
    public class DesktopWindowService
    {
        public IntPtr ProgmanHandle { get; private set; } = IntPtr.Zero;
        public IntPtr WorkerWHandle { get; private set; } = IntPtr.Zero;
        public bool IsDesktopAttached { get; private set; } = false;
        public string? FailureReason { get; private set; }

        /// <summary>
        /// Finds the Progman window and spawns WorkerW behind the desktop icons.
        /// </summary>
        public bool InitializeDesktopIntegration()
        {
            try
            {
                // Step 1: Find Progman
                ProgmanHandle = NativeMethods.FindWindow("Progman", null);
                if (ProgmanHandle == IntPtr.Zero)
                {
                    FailureReason = "Progman window not found.";
                    Debug.WriteLine(FailureReason);
                    return false;
                }

                // Step 2: Send 0x052C to Progman to spawn/ensure WorkerW exists
                NativeMethods.SendMessageTimeout(
                    ProgmanHandle,
                    NativeMethods.WM_052C,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.SMTO_NORMAL,
                    1000,
                    out _);

                // Step 3: Enumerate top-level windows to find WorkerW behind SHELLDLL_DefView
                IntPtr foundWorkerW = IntPtr.Zero;
                NativeMethods.EnumWindows((hwnd, lParam) =>
                {
                    IntPtr shellDllDefView = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellDllDefView != IntPtr.Zero)
                    {
                        // The WorkerW we want is the one AFTER the window containing SHELLDLL_DefView
                        foundWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                        return false; // stop enumeration
                    }
                    return true;
                }, IntPtr.Zero);

                if (foundWorkerW != IntPtr.Zero)
                {
                    WorkerWHandle = foundWorkerW;
                    IsDesktopAttached = true;
                    Debug.WriteLine($"Desktop integration initialized. WorkerW: 0x{foundWorkerW:X}");
                }
                else
                {
                    // Fallback: use Progman itself as the desktop host
                    WorkerWHandle = ProgmanHandle;
                    IsDesktopAttached = true;
                    FailureReason = "WorkerW not found after 0x052C; using Progman as fallback.";
                    Debug.WriteLine(FailureReason);
                }

                return IsDesktopAttached;
            }
            catch (Exception ex)
            {
                FailureReason = $"Exception during desktop init: {ex.Message}";
                Debug.WriteLine(FailureReason);
                IsDesktopAttached = false;
                return false;
            }
        }

        /// <summary>
        /// Attaches a WPF window to the desktop shell layer using the OWNER approach
        /// (GWLP_HWNDPARENT) which preserves WPF's DirectX rendering pipeline.
        /// </summary>
        public bool AttachWindow(Window window)
        {
            if (!IsDesktopAttached || WorkerWHandle == IntPtr.Zero)
            {
                if (!InitializeDesktopIntegration())
                    return false;
            }

            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;

            if (hwnd == IntPtr.Zero)
            {
                helper.EnsureHandle();
                hwnd = helper.Handle;
            }

            try
            {
                // Set OWNER to WorkerW (not PARENT — preserves WPF rendering)
                // This makes Win+D show/hide the note along with the desktop
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, WorkerWHandle);

                // Add WS_EX_TOOLWINDOW: prevents taskbar entry for this window
                IntPtr exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
                long style = exStyle.ToInt64();
                style |= NativeMethods.WS_EX_TOOLWINDOW;
                style &= ~NativeMethods.WS_EX_APPWINDOW; // remove APPWINDOW if present
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

                // Explicitly push it to the bottom behind normal windows
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, 
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

                Debug.WriteLine($"Attached window 0x{hwnd:X} to WorkerW 0x{WorkerWHandle:X}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to attach window: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detaches a window from the desktop shell (for Always-on-Top mode).
        /// </summary>
        public void DetachWindow(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            try
            {
                // Remove owner relationship
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, IntPtr.Zero);

                // Restore normal window style
                IntPtr exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
                long style = exStyle.ToInt64();
                style &= ~NativeMethods.WS_EX_TOOLWINDOW;
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to detach window: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-initializes desktop integration (e.g., after Explorer restart).
        /// </summary>
        public bool Reinitialize()
        {
            IsDesktopAttached = false;
            WorkerWHandle = IntPtr.Zero;
            ProgmanHandle = IntPtr.Zero;
            return InitializeDesktopIntegration();
        }
    }
}
