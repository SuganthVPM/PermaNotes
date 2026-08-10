using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using DesktopNotes.Interop;
using DesktopNotes.Models;
using DesktopNotes.Services;
using DesktopNotes.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DesktopNotes
{
    public partial class App : Application
    {
        // --- Constants ---
        private const string MutexName = "DesktopNotes_SingleInstance_F7A2B";
        private const int HOTKEY_ID_NEW_NOTE = 9001;
        private const int HOTKEY_ID_SEARCH = 9002;
        private const long MAX_LOG_SIZE = 512 * 1024; // 512 KB

        // --- State ---
        private Mutex? _singleInstanceMutex;
        private readonly List<NoteWindow> _activeNoteWindows = new();
        private readonly NoteStorageService _storageService = new();
        private readonly DesktopWindowService _desktopWindowService = new();
        private NotifyIcon? _trayIcon;
        private HwndSource? _hotkeyHwndSource;
        private uint _taskbarCreatedMessageId;
        private System.Timers.Timer? _memoryTimer;

        // --- Logging ---
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopNotes");
        private static readonly string TraceLogPath = Path.Combine(LogDir, "app_trace.log");

        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        // ===================== STARTUP =====================

        protected override void OnStartup(StartupEventArgs e)
        {
            // Force software rendering to lower GPU/Memory overhead
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            base.OnStartup(e);

            // --- Single-instance enforcement ---
            _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Perma Notes is already running.", "Perma Notes",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // --- Exception handlers ---
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Trace($"UnhandledException: {args.ExceptionObject}");
            DispatcherUnhandledException += (s, args) =>
            {
                Trace($"DispatcherUnhandledException: {args.Exception}");
                args.Handled = true;
            };

            // --- Log rotation ---
            RotateLogIfNeeded();
            Trace("App startup");

            try
            {
                // --- Desktop integration ---
                _desktopWindowService.InitializeDesktopIntegration();
                Trace($"Desktop integration: attached={_desktopWindowService.IsDesktopAttached}");

                // --- System tray icon ---
                InitializeTrayIcon();

                // --- Global hotkey (Ctrl+Alt+N) ---
                RegisterGlobalHotkey();

                // --- Load and display notes ---
                LoadNotes();

                // --- Memory Trimming ---
                _memoryTimer = new System.Timers.Timer(60000); // Trim every 1 minute
                _memoryTimer.Elapsed += (s, e) => TrimMemory();
                _memoryTimer.Start();
                TrimMemory(); // Trim immediately on startup

                Trace($"Active windows: {_activeNoteWindows.Count}");
            }
            catch (Exception ex)
            {
                Trace($"Exception in OnStartup: {ex}");
            }
        }

        // ===================== NOTE WINDOW LIFECYCLE =====================

        private void LoadNotes()
        {
            var savedNotes = _storageService.LoadNotes();
            Trace($"Loaded {savedNotes.Count} saved notes");

            if (savedNotes.Count == 0)
            {
                var settings = AppSettings.Load();
                var welcomeNote = new Note
                {
                    Title = "Welcome to Perma Notes",
                    Text = "Welcome to Perma Notes!\n\n"
                         + "• Stays visible on Win + D (Show Desktop)\n"
                         + "• Normal apps cover notes\n"
                         + "• Drag header to move\n"
                         + "• Double-click title to rename\n"
                         + "• Right-click for colors, opacity & settings!",
                    X = 200, Y = 180,
                    Width = 340, Height = 300,
                    BackgroundColor = settings.DefaultNoteColor,
                    Opacity = settings.DefaultOpacity
                };
                savedNotes.Add(welcomeNote);
                _storageService.SaveNotesImmediate(savedNotes);
            }

            ValidateNotePositions(savedNotes);

            foreach (var note in savedNotes)
            {
                CreateNoteWindowInstance(note);
            }
        }

        private void CreateNoteWindowInstance(Note note)
        {
            var window = new NoteWindow(note);

            // Attach to desktop when loaded
            window.Loaded += (s, _) =>
            {
                try
                {
                    if (!note.IsAlwaysOnTop)
                        _desktopWindowService.AttachWindow(window);
                }
                catch (Exception ex) { Trace($"Attach error: {ex.Message}"); }
            };

            // Debounced save on any change
            window.NoteChanged += (s, _) => OnNotesStateChanged();

            // Always-on-Top toggle: attach/detach from desktop
            window.AlwaysOnTopChanged += (s, _) =>
            {
                if (note.IsAlwaysOnTop)
                    _desktopWindowService.DetachWindow(window);
                else
                    _desktopWindowService.AttachWindow(window);
            };

            // Note actions
            window.RequestNewNote += (s, _) => SpawnNewNote(window);
            window.RequestDeleteNote += (s, w) => DeleteNoteWindow(w);
            window.RequestDuplicateNote += (s, clone) => CreateNoteWindowInstance(clone);
            window.RequestSettings += (s, _) => OpenSettings();

            _activeNoteWindows.Add(window);
            window.Show();
        }

        private void SpawnNewNote(NoteWindow? sourceWindow = null)
        {
            var settings = AppSettings.Load();
            var newNote = new Note
            {
                Title = "New Note",
                Text = string.Empty,
                X = (sourceWindow?.Left ?? 200) + 40,
                Y = (sourceWindow?.Top ?? 180) + 40,
                Width = 320,
                Height = 260,
                BackgroundColor = settings.DefaultNoteColor,
                Opacity = settings.DefaultOpacity
            };
            CreateNoteWindowInstance(newNote);
            OnNotesStateChanged();
        }

        private void DeleteNoteWindow(NoteWindow window)
        {
            _activeNoteWindows.Remove(window);
            window.Close();
            OnNotesStateChanged();
            // Don't shutdown — tray icon keeps app alive
        }

        private void ShowAllNotes()
        {
            if (_activeNoteWindows.Count == 0)
            {
                SpawnNewNote();
            }
            foreach (var w in _activeNoteWindows)
            {
                w.Show();
                w.WindowState = WindowState.Normal;
            }
        }

        private void HideAllNotes()
        {
            foreach (var w in _activeNoteWindows)
            {
                w.Hide();
            }
        }

        // ===================== PERSISTENCE =====================

        private void OnNotesStateChanged()
        {
            var allNotes = _activeNoteWindows.Select(w => w.NoteModel).ToList();
            _storageService.SaveNotesDebounced(allNotes);
        }

        // ===================== MULTI-MONITOR VALIDATION =====================

        /// <summary>
        /// Ensures notes aren't positioned off-screen (e.g. monitor was disconnected).
        /// Moves orphaned notes to the nearest visible monitor.
        /// </summary>
        private void ValidateNotePositions(List<Note> notes)
        {
            var screens = Screen.AllScreens;
            foreach (var note in notes)
            {
                var noteCenter = new NativeMethods.POINT
                {
                    X = (int)(note.X + note.Width / 2),
                    Y = (int)(note.Y + note.Height / 2)
                };

                bool isOnScreen = false;
                foreach (var screen in screens)
                {
                    if (screen.WorkingArea.Contains(noteCenter.X, noteCenter.Y))
                    {
                        isOnScreen = true;
                        break;
                    }
                }

                if (!isOnScreen)
                {
                    // Move to primary monitor
                    var primary = Screen.PrimaryScreen?.WorkingArea
                        ?? new Rectangle(0, 0, 1920, 1080);
                    note.X = primary.Left + 100;
                    note.Y = primary.Top + 100;
                    Trace($"Moved off-screen note {note.Id} to primary monitor");
                }
            }
        }

        // ===================== SYSTEM TRAY =====================

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Text = "Perma Notes",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            _trayIcon.ContextMenuStrip.Items.Add("New Note", null, (s, e) => Dispatcher.Invoke(() => SpawnNewNote()));
            _trayIcon.ContextMenuStrip.Items.Add("Search Notes", null, (s, e) => Dispatcher.Invoke(OpenSearch));
            _trayIcon.ContextMenuStrip.Items.Add("Show All Notes", null, (s, e) => Dispatcher.Invoke(ShowAllNotes));
            _trayIcon.ContextMenuStrip.Items.Add("Hide All Notes", null, (s, e) => Dispatcher.Invoke(HideAllNotes));
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Settings", null, (s, e) => Dispatcher.Invoke(OpenSettings));
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Export Notes", null, (s, e) => Dispatcher.Invoke(ExportNotes));
            _trayIcon.ContextMenuStrip.Items.Add("Import Notes", null, (s, e) => Dispatcher.Invoke(ImportNotes));
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Dispatcher.Invoke(ExitApp));

            _trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(ShowAllNotes);
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow();
            if (settingsWindow.ShowDialog() == true)
            {
                // Storage path was changed
                var newPath = AppSettings.Load().CustomStoragePath;
                if (string.IsNullOrWhiteSpace(newPath))
                {
                    newPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes");
                }
                
                _storageService.ChangeStorageDirectory(newPath);
                
                // Hide all current note windows
                foreach (var win in _activeNoteWindows.ToList())
                {
                    win.Close();
                }
                _activeNoteWindows.Clear();
                
                // Reload notes from the new path
                LoadNotes();
            }
        }

        private void OpenSearch()
        {
            var searchWindow = new SearchWindow(_activeNoteWindows);
            searchWindow.Show();
            searchWindow.Activate();
        }

        private void ExportNotes()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"DesktopNotes_Backup_{DateTime.Now:yyyyMMdd}"
            };
            
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sourceFile = Path.Combine(LogDir, "notes.json");
                    if (File.Exists(sourceFile))
                    {
                        File.Copy(sourceFile, sfd.FileName, true);
                        MessageBox.Show("Notes exported successfully.", "Export Notes", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No notes to export.", "Export Notes", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportNotes()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var importedNotes = System.Text.Json.JsonSerializer.Deserialize<List<Note>>(json);
                    
                    if (importedNotes != null && importedNotes.Count > 0)
                    {
                        var result = MessageBox.Show($"Found {importedNotes.Count} notes to import. Do you want to add them to your existing notes?", 
                            "Import Notes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            foreach (var note in importedNotes)
                            {
                                // Generate new ID to avoid conflicts
                                note.Id = Guid.NewGuid();
                                // Offset slightly so they don't exactly overlap existing notes
                                note.X += 20;
                                note.Y += 20;
                                
                                CreateNoteWindowInstance(note);
                            }
                            OnNotesStateChanged();
                            MessageBox.Show("Notes imported successfully.", "Import Notes", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed. Invalid file format or error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ===================== GLOBAL HOTKEY (Ctrl+Alt+N) =====================

        private void RegisterGlobalHotkey()
        {
            try
            {
                // Create a hidden window for receiving WM_HOTKEY messages
                var parameters = new HwndSourceParameters("DesktopNotesHotkeyWindow")
                {
                    Width = 0, Height = 0,
                    WindowStyle = 0 // hidden
                };
                _hotkeyHwndSource = new HwndSource(parameters);
                _hotkeyHwndSource.AddHook(HiddenWindowProc);

                // Register for Explorer restart message
                _taskbarCreatedMessageId = NativeMethods.RegisterWindowMessage("TaskbarCreated");

                bool registeredNewNote = NativeMethods.RegisterHotKey(
                    _hotkeyHwndSource.Handle,
                    HOTKEY_ID_NEW_NOTE,
                    NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                    NativeMethods.VK_N);

                bool registeredSearch = NativeMethods.RegisterHotKey(
                    _hotkeyHwndSource.Handle,
                    HOTKEY_ID_SEARCH,
                    NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                    0x53); // VK_S

                Trace($"Global hotkey Ctrl+Alt+N registered: {registeredNewNote}");
                Trace($"Global hotkey Ctrl+Alt+S registered: {registeredSearch}");
            }
            catch (Exception ex)
            {
                Trace($"Failed to register global hotkey: {ex.Message}");
            }
        }

        private IntPtr HiddenWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (hotkeyId == HOTKEY_ID_NEW_NOTE)
                {
                    SpawnNewNote();
                    handled = true;
                }
                else if (hotkeyId == HOTKEY_ID_SEARCH)
                {
                    OpenSearch();
                    handled = true;
                }
            }
            else if (msg == _taskbarCreatedMessageId)
            {
                // Explorer restarted, re-attach all notes
                Trace("Explorer restart detected. Reinitializing desktop integration.");
                if (_desktopWindowService.Reinitialize())
                {
                    foreach (var window in _activeNoteWindows)
                    {
                        if (!window.NoteModel.IsAlwaysOnTop)
                        {
                            _desktopWindowService.AttachWindow(window);
                        }
                    }
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        // ===================== LOG ROTATION =====================

        private void RotateLogIfNeeded()
        {
            try
            {
                if (File.Exists(TraceLogPath))
                {
                    var info = new FileInfo(TraceLogPath);
                    if (info.Length > MAX_LOG_SIZE)
                    {
                        var oldPath = TraceLogPath + ".old";
                        File.Delete(oldPath);
                        File.Move(TraceLogPath, oldPath);
                    }
                }
            }
            catch { }
        }

        public static void Trace(string message)
        {
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                File.AppendAllText(TraceLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        // ===================== EXIT =====================

        private void ExitApp()
        {
            // Save all notes immediately
            var allNotes = _activeNoteWindows.Select(w => w.NoteModel).ToList();
            _storageService.SaveNotesImmediate(allNotes);

            // Cleanup
            try
            {
                if (_hotkeyHwndSource?.Handle != IntPtr.Zero)
                {
                    NativeMethods.UnregisterHotKey(_hotkeyHwndSource!.Handle, HOTKEY_ID_NEW_NOTE);
                    NativeMethods.UnregisterHotKey(_hotkeyHwndSource!.Handle, HOTKEY_ID_SEARCH);
                }
                _hotkeyHwndSource?.Dispose();
            }
            catch { }

            _trayIcon?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            Trace("App exiting");
            Shutdown();
        }

        // ===================== MEMORY MANAGEMENT =====================

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        private void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
