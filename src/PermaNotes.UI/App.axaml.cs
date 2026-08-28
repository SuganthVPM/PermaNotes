using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PermaNotes.Core.Services;
using PermaNotes.UI.ViewModels;
using PermaNotes.UI.Views;

namespace PermaNotes.UI
{
    public partial class App : Application
    {
        private readonly IClickThroughService? _clickThroughService;
        private readonly IDesktopPinService?   _desktopPinService;
        private readonly IStartupService?      _startupService;
        private readonly IVibrancyService?     _vibrancyService;

        private AppViewModel _appViewModel = null!;
        private readonly Dictionary<Guid, NoteWindow> _noteWindows = new();
        private NoteManagerWindow? _managerWindow;
        private SettingsWindow? _settingsWindow;

        /// <summary>Parameterless constructor for XAML designer / default AppBuilder path.</summary>
        public App() { }

        /// <summary>Constructor used by Program.cs when platform services are available.</summary>
        public App(
            IClickThroughService? clickThroughService,
            IDesktopPinService?   desktopPinService,
            IStartupService?      startupService,
            IVibrancyService?     vibrancyService = null)
        {
            _clickThroughService = clickThroughService;
            _desktopPinService   = desktopPinService;
            _startupService      = startupService;
            _vibrancyService     = vibrancyService;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _appViewModel = new AppViewModel(
                    clickThroughService: _clickThroughService,
                    desktopPinService:   _desktopPinService,
                    startupService:      _startupService);

                // Initialize Tray Icon immediately so it appears in the taskbar/tray with zero delay
                SetupTrayIcon(desktop);

                _appViewModel.NoteCreated += (s, noteVm) => ShowNoteWindow(noteVm);
                _appViewModel.NoteClosed += (s, noteVm) => HideNoteWindow(noteVm);
                _appViewModel.NoteDeleted += (s, noteVm) => CloseNoteWindow(noteVm);
                _appViewModel.RequestOpenManager += (s, e) => OpenNoteManager();
                _appViewModel.RequestOpenSettings += (s, e) => OpenSettings();

                // Open existing non-closed notes
                var notesToShow = _appViewModel.Notes.Where(n => !n.IsClosed).ToList();
                if (notesToShow.Count == 0 && _appViewModel.Notes.Count > 0)
                {
                    notesToShow.Add(_appViewModel.Notes[0]);
                    _appViewModel.Notes[0].IsClosed = false;
                }

                foreach (var note in notesToShow)
                {
                    ShowNoteWindow(note);
                }

                if (_noteWindows.Count > 0)
                {
                    desktop.MainWindow = _noteWindows.Values.First();
                }

                ScheduleMemoryTrim();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ShowNoteWindow(NoteViewModel vm)
        {
            if (!_noteWindows.TryGetValue(vm.Id, out var window))
            {
                window = new NoteWindow(vm, _clickThroughService, _desktopPinService, _vibrancyService);
                window.RequestNewNote += (s, e) => _appViewModel.NewNote();
                window.RequestOpenManager += (s, e) => OpenNoteManager();
                window.RequestDuplicateNote += (s, e) => _appViewModel.DuplicateNote(vm);
                window.RequestDeleteNote += (s, e) => _appViewModel.DeleteNote(vm);

                _noteWindows[vm.Id] = window;
                window.Closed += (s, e) => _noteWindows.Remove(vm.Id);
            }

            window.Show();
            window.Activate();
        }

        private void HideNoteWindow(NoteViewModel vm)
        {
            if (_noteWindows.TryGetValue(vm.Id, out var window))
            {
                window.Hide();
            }
        }

        private void CloseNoteWindow(NoteViewModel vm)
        {
            if (_noteWindows.TryGetValue(vm.Id, out var window))
            {
                _noteWindows.Remove(vm.Id);
                window.Close();
            }
        }

        private void OpenNoteManager()
        {
            if (_managerWindow == null)
            {
                var vm = new NoteManagerViewModel(_appViewModel);
                _managerWindow = new NoteManagerWindow(vm);
                _managerWindow.NoteActivated += (s, noteVm) => ShowNoteWindow(noteVm);
                _managerWindow.Closed += (s, e) => _managerWindow = null;
                _managerWindow.Show();
            }
            else
            {
                _managerWindow.Activate();
            }
        }

        private void OpenSettings()
        {
            if (_settingsWindow == null)
            {
                var vm = new SettingsViewModel(_startupService);
                _settingsWindow = new SettingsWindow(vm);
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var menu = new NativeMenu();

                var newNoteItem = new NativeMenuItem("New Note");
                newNoteItem.Click += (s, e) => _appViewModel.NewNote();
                menu.Add(newNoteItem);

                var managerItem = new NativeMenuItem("Note Manager");
                managerItem.Click += (s, e) => OpenNoteManager();
                menu.Add(managerItem);

                menu.Add(new NativeMenuItemSeparator());

                var showAllItem = new NativeMenuItem("Show All Notes");
                showAllItem.Click += (s, e) => _appViewModel.ShowAllNotes();
                menu.Add(showAllItem);

                var hideAllItem = new NativeMenuItem("Hide All Notes");
                hideAllItem.Click += (s, e) => _appViewModel.HideAllNotes();
                menu.Add(hideAllItem);

                menu.Add(new NativeMenuItemSeparator());

                var settingsItem = new NativeMenuItem("Settings");
                settingsItem.Click += (s, e) => OpenSettings();
                menu.Add(settingsItem);

                menu.Add(new NativeMenuItemSeparator());

                var exitItem = new NativeMenuItem("Exit");
                exitItem.Click += (s, e) =>
                {
                    _appViewModel.SaveImmediate();
                    desktop.Shutdown();
                };
                menu.Add(exitItem);

                WindowIcon? icon = null;
                try
                {
                    icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://PermaNotes.UI/Assets/icon.ico")));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
                }

                var trayIcon = new TrayIcon
                {
                    ToolTipText = "PermaNotes",
                    Menu = menu,
                    Icon = icon,
                    IsVisible = true
                };

                trayIcon.Clicked += (s, e) => OpenNoteManager();

                var icons = new TrayIcons { trayIcon };
                TrayIcon.SetIcons(this, icons);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tray icon initialization: {ex.Message}");
            }
        }

        private void ScheduleMemoryTrim()
        {
            // Reclaim unneeded JIT and assembly-load pages 3 seconds after startup,
            // and periodically every 45s to flush working set when idle
            var timer = new System.Threading.Timer(_ =>
            {
                TrimMemory();
            }, null, 3000, 45000);
        }

        private static void TrimMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    try
                    {
                        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
                    }
                    catch { }
                }
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);
    }
}
