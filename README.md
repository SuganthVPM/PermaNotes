# PermaNotes

**Always-on-desktop sticky notes for Windows** — lightweight, offline, and built to stay out of your way.

A lightweight, always-on-desktop sticky note app for Windows. Notes behave like desktop gadgets — they remain visible when you press **Win + D** (Show Desktop), but normal application windows draw over them.

PermaNotes pins rich-text sticky notes directly to your Windows desktop layer, so they're always visible (even alongside your icons, shown on Win+D) but never in the way of your open windows. Built in WPF on .NET 8, fully local — no accounts, no cloud sync, no telemetry.

## Features
- **Always-on-Desktop**: Notes sit above wallpaper icons but below active application windows (survives Win+D).
- **Live Markdown & Rich Text**: Rich text editing with instant markdown expansions (`#`, `---`, `**bold**`, `==highlight==`, `-->`).
- **Interactive Checklists**: Clickable task lists (`☐` / `☑`) with single-click toggling and smart keyboard triggers (`[] `).
- **8-Way Smooth Resizing**: Hardware-accelerated perimeter resizing (all 4 edges and 4 corners) with zero flicker or DWM glitches.
- **Smart Paste Sanitization**: Automatically strips foreign background fills from external apps while preserving intentional highlights.
- **Adaptive Contrast & Custom Caret**: High-contrast I-Beam cursor and dynamic text contrast that adapts to light/dark note themes.
- **System Tray & Note Manager**: Quick tray access, global hotkeys, search, and centralized note management.
- **100% Offline & Private**: Local atomic JSON persistence with debounced auto-save — your notes never leave your PC.

## Why PermaNotes?
Unlike Windows Sticky Notes or browser-based note apps, PermaNotes notes live directly on your desktop layer, so they're always visible without cluttering your taskbar or stealing window focus.

```
Normal Windows (Chrome / VS Code / Explorer)
              ↓
        PermaNotes  ← visible on Win+D, hidden under apps
              ↓
        Desktop Wallpaper
```

---

## ✨ Feature Overview

### Core Behaviour
| Feature | Detail |
|---|---|
| **Desktop persistence** | Notes survive Win+D and sit behind normal windows via the Progman/WorkerW shell technique |
| **8-Way Perimeter Resizing** | Grab any of the 4 edges or 4 corners to resize smoothly without DWM flicker; enforced min dimensions (200×150 px) |
| **Multiple notes** | Each note has its own position, size, colour, opacity, and content |
| **Auto-save** | Debounced (500 ms) atomic writes — no data loss on crash |
| **Position/size restore** | All notes reopen exactly where you left them across multiple monitors |
| **Single-instance** | Only one copy of PermaNotes runs at a time |
| **No admin required** | Runs entirely in user space |
| **No network access** | Completely local — no telemetry |

---

### Rich Text & Live Markdown Engine
| Feature | Detail |
|---|---|
| **Live Markdown Triggers** | Instant formatting on `Space` or `Enter` for headings (`# `), dividers (`---`), task boxes (`[] `), formatting (`**bold**`), and smart arrows (`-->` to `→`) |
| **Interactive Checklists** | Real-time checkboxes (`☐` / `☑`) with pixel-perfect hitboxes and dedicated hover cursor detection |
| **Smart Paste Sanitization** | Intercepts clipboard paste to strip foreign white/dark background fills from external tools (browsers, IDEs) while preserving yellow highlights |
| **Bold / Italic / Underline** | Standard formatting via toolbar, context menu, or standard Ctrl shortcuts |
| **Strikethrough** | Toggle via toolbar button (S̶), context menu, or `Ctrl+Shift+X` (also via `~~text~~`) |
| **Highlight** | Toggles yellow highlight on selection — `Ctrl+H`, toolbar, context menu, or `==text==` |
| **Font size** | Absolute sizes (10–32 px) via context menu; relative ±2 via toolbar A↑/A↓ |
| **Bullet list** | One-click bullet list in the toolbar |
| **Undo / Redo** | `Ctrl+Z` / `Ctrl+Y` — also exposed as ↶/↷ buttons in the popup toolbar |
| **Insert Timestamp** | Inserts `yyyy-MM-dd HH:mm` at the caret — `Ctrl+T`, toolbar, or context menu |

### Formatting Popup
A floating toolbar appears automatically whenever you select text. It includes:
**B** · _I_ · <u>U</u> · S̶ · 🖍 Highlight · 🔵 Bullets · A↑ · A↓ · ↶ · ↷ · 🕐

---

### Note Management
| Feature | Detail |
|---|---|
| **Note Manager** | Central dashboard listing all notes (open + closed) with search, colour dot, status, and last-updated timestamp. Double-click to activate a note. |
| **New Note** | Header button, context menu, tray icon, or `Ctrl+N` |
| **Duplicate** | Context menu → Duplicate — clones content and position |
| **Close note** | Hides the note (not deleted); reopen from Note Manager |
| **Delete note** | Context menu → Delete Note — permanent, with confirmation prompt |
| **Export as text** | Context menu → Export as Text File — saves plain text to a file |
| **Lock note** | Makes the note read-only; disables all editing, resizing, and the format popup. `Ctrl+Shift+L` or the 🔒 header button |

---

### Appearance & UX
| Feature | Detail |
|---|---|
| **9 preset colours** | Yellow, Orange, Green, Teal, Blue, Purple, Pink, White, Gray |
| **Custom colour** | Full colour wheel picker (context menu → Color → Custom Color…) |
| **Opacity control** | 100% down to 30% in 10% steps (context menu → Opacity) |
| **Adaptive contrast** | Text and icon colours automatically switch to white on dark backgrounds |
| **High-Contrast I-Beam** | Custom high-contrast text caret that isolates over scrollbars and checkbox controls |
| **Slim Expanding Scrollbar** | Ultra-thin idle profile that smoothly widens on hover/drag for effortless grabbing |
| **Adaptive border** | Border colour is derived from the note's background for a cohesive look |
| **Always on Top** | Per-note toggle — floats above all windows; detached from desktop layer |

---

### Keyboard Shortcuts & Markdown Syntax

#### ⌨️ Keyboard Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl+N` | New note |
| `Ctrl+H` | Toggle highlight on selected text |
| `Ctrl+Shift+X` | Toggle strikethrough on selected text |
| `Ctrl+Shift+L` | Toggle note lock (read-only) |
| `Ctrl+T` | Insert timestamp at caret |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+U` | Bold / Italic / Underline |
| `Ctrl+Alt+N` | **Global** — create new note from anywhere |

#### ⚡ Live Markdown Triggers (Triggered on Space / Enter)
| Syntax Trigger | Result |
|---|---|
| `# ` + Space | Heading 1 (Large Bold) |
| `## ` + Space | Heading 2 (Medium Bold) |
| `### ` + Space | Heading 3 (Subheading) |
| `[] ` or `[ ] ` + Space | Interactive Unchecked Task Box (`☐ `) |
| `[x] ` + Space | Interactive Checked Task Box (`☑ `) |
| `---` or `***` + Enter | Inline Horizontal Divider Rule |
| `**text**` + Space | **Bold** text |
| `*text*` + Space | *Italic* text |
| `~~text~~` + Space | ~~Strikethrough~~ text |
| `==text==` + Space | Yellow highlighted text |
| `[text](url)` + Space | Hyperlink |
| `-->` / `==>` / `--->` + Space | Smart arrows (`→`, `⇒`, `⟶`) |

---

### System Integration
| Feature | Detail |
|---|---|
| **System tray icon** | Right-click for: New Note, Note Manager, Show/Hide All, Settings, Exit |
| **Global hotkey** | `Ctrl+Alt+N` creates a new note even when PermaNotes is in the background |
| **Start with Windows** | Optional toggle in Settings (written to HKCU registry — no admin required) |
| **Per-Monitor V2 DPI** | Crisp rendering at 100%, 125%, 150%, 200% scale |
| **Multi-monitor support** | Notes live on any monitor; orphaned notes auto-relocate to the primary |
| **Explorer restart guard** | Re-attaches all note windows to WorkerW when Windows Explorer restarts |

---

## Requirements

- Windows 10 / 11 (x64)
- .NET 8.0 Runtime (bundled in self-contained release builds)

---

## Build & Run

### Install .NET 8 SDK (user-level, no admin)
```powershell
powershell -ExecutionPolicy Bypass -Command "& { iwr https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1; .\dotnet-install.ps1 -Channel 8.0 }"
```

Or use the user-local dotnet if already installed:
```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run
```

### Development run
```powershell
dotnet run
```

### Publish standalone executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/
```
Or run the automated build script (which automatically terminates running instances and packages the output):
```powershell
.\build.ps1
```
Then run `dist/PermaNotes.exe` — no .NET installation needed on the target machine.

---

## Architecture

```
DesktopNotes/
├── App.xaml / App.xaml.cs              # Central controller: tray, hotkeys, note lifecycle, persistence
├── Models/
│   └── Note.cs                         # Note data model (Id, Title, RtfText, position, colour, flags)
├── Views/
│   ├── NoteWindow.xaml/.cs             # Sticky note window — 8-way resize, rich text, live markdown
│   ├── NoteManagerWindow.xaml/.cs      # Note Manager dashboard — search, open/close, delete
│   ├── SettingsWindow.xaml/.cs         # Settings dialog + AppSettings persistence
│   └── SearchWindow.xaml/.cs          # Full-text search across all notes
├── Services/
│   ├── DesktopWindowService.cs         # Progman/WorkerW shell integration
│   ├── NoteStorageService.cs           # Atomic JSON persistence with debounced auto-save
│   └── StartupService.cs              # "Start with Windows" via HKCU registry
├── Interop/
│   └── NativeMethods.cs               # Win32 P/Invoke declarations
├── app.manifest                        # No-admin, Windows 10/11 compatibility declaration
└── docs/
    ├── index.html                      # Promotional website & download portal
    └── desktop-integration.md          # Technical deep-dive on shell integration
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| `ShutdownMode.OnExplicitShutdown` | App stays alive via tray even when all note windows are closed |
| Event-based window ↔ App communication | `NoteWindow` raises events; `App` wires them up — clean separation of concerns |
| Progman/WorkerW owner (not SetParent) | Preserves WPF's DirectX rendering pipeline while keeping notes on the desktop layer |
| Debounced save (500 ms) | Avoids hammering disk on every keystroke while keeping data fresh |
| RTF + plain-text storage | RTF is the canonical format; plain text is kept for search/preview |
| TextPointer walk for highlight detection | `GetPropertyValue` is unreliable on mixed-format RTF selections; run-level walking is accurate |
| Custom 8-Way Thumb Grips | Replaces `WindowChrome` resizing to prevent DWM composition flickers and crash bugs on transparent windows |

---

## Desktop Integration

The app uses the **Progman/WorkerW owner technique**:

1. Sends message `0x052C` to `Progman` to spawn a `WorkerW` behind desktop icons.
2. Sets each note window's **owner** (not parent) to the `WorkerW` via `GWLP_HWNDPARENT`.
3. This makes notes follow the desktop's show/hide behavior during **Win + D**.

Using the *owner* relationship (not `SetParent`) preserves WPF's DirectX rendering pipeline and avoids z-order artifacts.

See [docs/desktop-integration.md](docs/desktop-integration.md) for full technical details.

---

## Data Storage

| File | Location | Purpose |
|---|---|---|
| `notes.json` | `%LOCALAPPDATA%\DesktopNotes\` | All note data (RTF content, positions, colours, flags) |
| `settings.json` | `%LOCALAPPDATA%\DesktopNotes\` | App-wide settings (default colour, opacity, startup, storage path) |
| `app_trace.log` | `%LOCALAPPDATA%\DesktopNotes\` | Debug trace log (auto-rotated at 512 KB) |

---

## Known Limitations

- Desktop integration relies on undocumented Windows shell internals (Progman/WorkerW) which could change in future Windows updates.
- Notes appear on the virtual desktop where they were created; virtual desktop switching is not explicitly managed.
- The `GWLP_HWNDPARENT` owner approach may not survive all Win+D edge cases on all Windows 11 builds.

---

## License

Private project — all rights reserved.
