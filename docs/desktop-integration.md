# Desktop Integration Strategy & Documentation

## Overview

The defining requirement of the Desktop Persistent Notepad application is that notes behave like desktop sticky-note widgets:
1. Notes stay visible when Windows executes **Show Desktop (`Win + D`)**.
2. Notes remain **underneath normal application windows** (Chrome, VS Code, Explorer cover notes).
3. Notes are **NOT** implemented using `TopMost = true` / `WS_EX_TOPMOST` by default.

---

## Desired Stacking Order

```text
Normal Application Windows (Chrome, VS Code, Explorer)
          ↓
Desktop Notes (WPF Note Windows)
          ↓
Desktop / Wallpaper (Progman / WorkerW)
```

---

## Win32 Shell Architecture & Technique Selection

### The Progman / WorkerW Desktop Hierarchy

On Windows 10 and 11, the desktop desktop wallpaper and icons live in a shell window hierarchy managed by `explorer.exe`:

```text
Progman ("Program Manager", class: Progman)
  └── SHELLDLL_DefView (FolderView containing desktop icons)
```

When wallpaper transitions, Live Wallpapers, or `Win + D` operations occur, Windows spawns a `WorkerW` window directly behind `SHELLDLL_DefView`.

Sending `0x052C` (undocumented message) to `Progman` forces Windows to spawn a `WorkerW` window between the wallpaper layer and desktop icons.

### Selected Win32 Approach

1. Find `Progman` window handle via `FindWindow("Progman", null)`.
2. Send message `0x052C` to `Progman` using `SendMessageTimeout`.
3. Enumerate top-level windows (`EnumWindows`) to find the spawned `WorkerW` window that contains `SHELLDLL_DefView` (or sits directly behind it).
4. Re-parent WPF note windows using `SetParent(noteHwnd, workerWHwnd)` OR manage Z-order using `SetWindowLongPtr` (`WS_EX_TOOLWINDOW`, `WS_CHILD`) and `SetWindowPos`.

---

## Recovery & Robustness Features

1. **Explorer Restart Recovery:** Listen for `RegisterWindowMessage("TaskbarCreated")`. If Explorer restarts, re-query Progman/WorkerW and re-attach all notes.
2. **Fallback Compatibility Mode:** If WorkerW cannot be acquired, fall back to normal window operation without crashing, displaying a non-modal warning log.
3. **Multi-Monitor & DPI:** Account for virtual screen bounds across monitors and per-monitor DPI scaling (`PerMonitorV2`).

---

## Tested Windows Builds

*(To be updated after empirical verification during Phase 3)*
