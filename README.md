# Desktop Persistent Notepad

A lightweight Windows desktop notepad application where notes behave like desktop widgets — they stay visible when you press **Win + D** (Show Desktop) but sit underneath normal application windows.

## ✨ Key Feature

```
Normal Windows (Chrome / VS Code / Explorer)
                  ↓
          Desktop Notes ← visible on Win+D
                  ↓
          Desktop Wallpaper
```

Notes are **NOT** always-on-top. They live at the desktop level, just like Windows gadgets used to work.

## Features

### Core
- **Desktop persistence** — Notes survive Win+D and stay behind normal windows
- **Multiple independent notes** — Each with its own position, size, color, and content
- **Auto-save** — Debounced (500ms) atomic writes to `%LOCALAPPDATA%\DesktopNotes\notes.json`
- **Crash-safe storage** — Atomic write strategy (tmp → rename) prevents data loss
- **Position/size persistence** — All notes restore exactly where you left them

### UI & Interaction
- **Drag to move** — Click and drag the title bar
- **Resize** — Drag the bottom-right corner grip
- **Double-click title to rename** — Clean inline editing with Enter to confirm, Escape to cancel
- **6 note colors** — Yellow, Blue, Green, Pink, White, Gray (right-click menu)
- **Opacity control** — 100%, 85%, 70%, 50% (right-click menu)
- **Duplicate notes** — Right-click → Duplicate
- **Delete with confirmation** — Prevents accidental deletion

### System Integration
- **System tray icon** — Right-click for New Note, Show/Hide All, Settings, Exit
- **Global hotkey** — `Ctrl+Alt+N` creates a new note from anywhere
- **Start with Windows** — Optional toggle in Settings (no admin required)
- **Single-instance** — Only one copy runs at a time
- **Per-Monitor V2 DPI** — Crisp rendering at any scale (100%, 125%, 150%, 200%)
- **Multi-monitor support** — Notes can live on any monitor; orphaned notes auto-relocate
- **Optional Always-on-Top** — Per-note toggle (right-click → Always on Top)
- **No admin privileges** — Runs entirely in user space
- **No network access** — Completely local, no telemetry

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 Runtime (bundled in self-contained build)

## Build & Run

### Prerequisites
```powershell
# Install .NET 8 SDK (user-level, no admin)
powershell -ExecutionPolicy Bypass -Command "& { iwr https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1; .\dotnet-install.ps1 -Channel 8.0 }"
```

### Build
```powershell
dotnet build
```

### Run (development)
```powershell
dotnet run
```

### Publish standalone executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/
```
Then run `dist/DesktopNotes.exe` — no .NET installation needed.

## Architecture

```
DesktopNotes/
├── App.xaml / App.xaml.cs          # Application lifecycle, tray icon, hotkeys
├── Models/
│   └── Note.cs                     # Note data model (Id, Title, Text, position, color...)
├── Views/
│   ├── NoteWindow.xaml/.cs         # Sticky note window (drag, resize, edit, context menu)
│   └── SettingsWindow.xaml/.cs     # Settings dialog + AppSettings persistence
├── Services/
│   ├── DesktopWindowService.cs     # Progman/WorkerW desktop shell integration
│   ├── NoteStorageService.cs       # Atomic JSON persistence with debounced autosave
│   └── StartupService.cs          # "Start with Windows" via HKCU registry
├── Interop/
│   └── NativeMethods.cs           # Win32 P/Invoke declarations
├── app.manifest                    # No-admin, Windows 10/11 compatibility
└── docs/
    └── desktop-integration.md      # Technical documentation of shell integration
```

## Desktop Integration

The app uses the **Progman/WorkerW owner technique**:
1. Sends message `0x052C` to `Progman` to spawn a `WorkerW` behind desktop icons
2. Sets note windows' **owner** (not parent) to `WorkerW` via `GWLP_HWNDPARENT`
3. This makes notes follow the desktop's show/hide behavior during Win+D

Using owner (not `SetParent`) preserves WPF's DirectX rendering pipeline.

See [docs/desktop-integration.md](docs/desktop-integration.md) for full technical details.

## Data Storage

| File | Location | Purpose |
|---|---|---|
| `notes.json` | `%LOCALAPPDATA%\DesktopNotes\` | All note data |
| `settings.json` | `%LOCALAPPDATA%\DesktopNotes\` | App settings |
| `app_trace.log` | `%LOCALAPPDATA%\DesktopNotes\` | Debug log (auto-rotated at 512KB) |

## Known Limitations

- Desktop integration relies on undocumented Windows shell behavior (Progman/WorkerW) which could change in future Windows updates
- The `GWLP_HWNDPARENT` owner approach may not survive all Win+D edge cases on all Windows 11 builds
- Virtual desktop behavior is not explicitly managed (notes appear on the desktop where created)

## License

Private project — all rights reserved.
