# Desktop Persistent Notepad for Windows — Gemini Pro Build Specification

## 1. Project Goal

Build a small, lightweight Windows desktop notepad application that behaves like a classic desktop sticky-note/gadget.

The defining behavior is:

- Notes are normal desktop objects, not conventional always-on-top windows.
- Notes stay visible when Windows uses **Show Desktop (`Win + D`)**.
- Notes remain **behind normal application windows**.
- Opening Chrome, Explorer, VS Code, etc. should cover the notes.
- Returning to the desktop should reveal the notes.
- Notes should remember their position, size, content, and appearance after restarting Windows/the application.

This is **NOT** an Always-on-Top application.

The desired stacking behavior is:

```text
Normal application windows
        ↓
Desktop notes
        ↓
Desktop / wallpaper
```

Do not solve the requirement by simply setting the note window to `TopMost = true`.

---

# 2. Development Environment

Target platform:

- Windows 10/11
- x64 initially
- Windows-only application

Preferred technology:

- C#
- .NET 8 or newer LTS-compatible version available in the environment
- WinUI 3 / Windows App SDK for the UI
- Win32 interop where required for desktop-level window behavior

If WinUI 3 creates unnecessary technical problems for the desktop-window integration, you may use a simpler Windows desktop UI technology such as WPF, provided the final application is modern, lightweight, and reliable.

Do NOT use Electron unless there is a compelling technical reason.

The application should be completely local.

No cloud backend.
No account.
No telemetry.
No external server.
No unnecessary dependencies.

---

# 3. Core Requirement — Desktop Persistence

This is the most important requirement.

The note must behave like a desktop widget.

Expected behavior:

### Normal state

```text
+------------------------------------------------+
| Chrome / Explorer / VS Code                    |
|                                                |
|       +----------------------+                 |
|       | My Note              |                 |
|       |                      |                 |
|       | Remember this       |                 |
|       +----------------------+                 |
|                                                |
+------------------------------------------------+
```

The note is underneath normal application windows.

If another application occupies the same screen area, that application covers the note.

### Press Win + D

```text
+------------------------------------------------+
|                                                |
|       +----------------------+                 |
|       | My Note              |                 |
|       |                      |                 |
|       | Remember this       |                 |
|       +----------------------+                 |
|                                                |
|                    Desktop                     |
+------------------------------------------------+
```

The note remains visible.

### Open another application

```text
+------------------------------------------------+
| Chrome                                         |
|                                                |
|   Chrome covers the note if they overlap       |
|                                                |
+------------------------------------------------+
```

The note must NOT float above Chrome.

---

# 4. IMPORTANT — Do Not Use Always-on-Top

Never implement the primary desktop persistence behavior using:

```text
TopMost = true
WS_EX_TOPMOST
Always-on-top
```

unless it is implemented only as an optional user setting.

The default mode must be:

```text
Desktop persistent = ON
Always on top = OFF
```

The note should live at the desktop/Shell level.

Investigate appropriate Win32 techniques involving the Windows Shell/desktop window hierarchy, such as:

- Progman
- WorkerW
- Shell desktop windows
- Win32 window styles
- SetParent / re-parenting where appropriate
- DWM behavior
- Show Desktop behavior
- Z-order management

Do not blindly copy an old WorkerW hack.

First investigate how the current Windows 10/11 desktop behaves and select the least fragile implementation.

---

# 5. Architecture

Keep the architecture simple.

Suggested structure:

```text
DesktopNotes/
│
├── App.xaml
├── App.xaml.cs
│
├── MainWindow.xaml
├── MainWindow.xaml.cs
│
├── Models/
│   └── Note.cs
│
├── Services/
│   ├── NoteStorageService.cs
│   ├── DesktopWindowService.cs
│   └── StartupService.cs
│
├── ViewModels/
│   └── NoteViewModel.cs
│
├── Views/
│   └── NoteWindow.xaml
│
├── Interop/
│   ├── NativeMethods.cs
│   └── ShellInterop.cs
│
└── Data/
    └── notes.json
```

Do not over-engineer the project.

---

# 6. MVP Features

The first working version must contain only these features:

## 6.1 Create a note

User can create a new note.

Default:

- Width: approximately 300 px
- Height: approximately 250 px
- Center or sensible location on primary monitor
- Editable text

## 6.2 Edit text

The main area of the note is a multiline text editor.

Changes should be saved automatically.

## 6.3 Move note

The user can drag the note around the desktop.

## 6.4 Resize note

The user can resize the note.

## 6.5 Delete note

Provide a simple delete action.

Ask for confirmation only if appropriate.

## 6.6 Persistent storage

Save:

- Note ID
- Text
- Position
- Size
- Title
- Background
- Text formatting settings if implemented

Use a local JSON file initially.

Example:

```json
{
  "notes": [
    {
      "id": "7b3f...",
      "title": "Work",
      "text": "Finish Pega challenge",
      "x": 120,
      "y": 240,
      "width": 320,
      "height": 260,
      "background": "#FFF59D"
    }
  ]
}
```

Store the file under the user's local application-data directory rather than inside the installation directory.

---

# 7. Note UI

The note should look like a clean modern sticky note.

Suggested layout:

```text
┌────────────────────────────────┐
│  Work                     ⋮ ×  │
├────────────────────────────────┤
│                                │
│  Finish today's tasks          │
│                                │
│  • Pega                       │
│  • Git                         │
│  • Email                       │
│                                │
│                                │
└────────────────────────────────┘
```

The title bar should be minimal.

Do not create a huge conventional application title bar.

The note should feel like a widget.

---

# 8. Window Appearance

Default:

- Borderless/minimal window
- Rounded corners if technically reliable
- Subtle shadow
- Light background
- Dark text
- No taskbar clutter for every individual note if possible
- No unnecessary chrome

The note must still be easy to move and resize.

Do not sacrifice usability just for visual appearance.

---

# 9. Note Interaction

Implement:

### Left click + drag

Drag the note.

### Text area

Click inside the note to edit.

### Right click

Context menu:

```text
New Note
Duplicate
Delete
----------------
Color
Opacity
----------------
Always on Top
----------------
Settings
```

Only implement options that are actually working.

Do not create fake UI controls.

---

# 10. Optional Always-on-Top Mode

After the desktop-persistent MVP works, add an optional setting:

```text
Always on Top
```

When enabled:

```text
Note
 ↓
Normal applications
 ↓
Desktop
```

When disabled:

```text
Normal applications
 ↓
Note
 ↓
Desktop
```

This feature is secondary.

Do not let it interfere with the default desktop-widget behavior.

---

# 11. Multiple Notes

The application must support multiple independent notes.

Example:

```text
+-------------+       +----------------+
| Shopping    |       | Work           |
|             |       |                |
| Milk        |       | Finish Pega    |
| Eggs        |       |                |
| Bread       |       |                |
+-------------+       +----------------+
```

Each note must have:

- Unique ID
- Independent position
- Independent size
- Independent content
- Independent color
- Independent settings

---

# 12. Application Startup

Provide an option:

```text
Start with Windows
```

When enabled:

- Launch the application after user login.
- Restore all saved notes.
- Restore their previous positions/sizes.

Do not require administrator privileges.

Prefer the standard per-user Windows startup mechanism.

---

# 13. Multiple Monitor Support

The application should support multiple monitors.

A note can exist on any connected monitor.

Store coordinates correctly.

Important:

- Do not assume monitor 0 is always the primary monitor.
- Do not assume all monitors have the same DPI.
- Handle negative screen coordinates.
- Handle monitors being disconnected.

Example:

```text
Monitor 1
┌──────────────────────┐
│       Note A         │
└──────────────────────┘

Monitor 2
┌──────────────────────────────┐
│              Note B          │
└──────────────────────────────┘
```

If a monitor disappears, move orphaned notes back onto a visible monitor rather than losing them.

---

# 14. DPI Awareness

The application must be DPI-aware.

Test with:

- 100%
- 125%
- 150%
- 200%

Do not allow notes to jump to unexpected locations because of DPI scaling.

Store and restore positions using the correct coordinate system.

---

# 15. Virtual Desktop Considerations

Initially, do not over-engineer virtual desktop support.

First make the application work correctly on the normal Windows desktop.

After the MVP is stable, investigate:

- Windows virtual desktops
- Whether notes should appear on every virtual desktop
- Whether notes should remain only on the desktop where created

For MVP, document the current behavior instead of pretending to support something that has not been tested.

---

# 16. Explorer Restart Handling

The desktop Shell can restart independently of the application.

The application should eventually detect if the desktop Shell/window hierarchy has changed.

If the desktop host window disappears:

1. Detect the change.
2. Reacquire the appropriate desktop/Shell window.
3. Reattach/reposition the notes.
4. Do not lose note data.

This is a later robustness feature, not a reason to block the initial MVP.

---

# 17. Win + D Testing

This must be an explicit test.

Test:

1. Launch the application.
2. Create a note.
3. Place it on the desktop.
4. Open Chrome.
5. Ensure Chrome covers the note when overlapping.
6. Press Win + D.
7. Confirm the note is visible.
8. Press Win + D again.
9. Confirm normal application windows return and cover the note.
10. Repeat several times.

Expected:

```text
Chrome
  ↓
Note
  ↓
Desktop
```

NOT:

```text
Note
  ↓
Chrome
```

---

# 18. Other Window Tests

Test against:

- File Explorer
- Chrome/Firefox/Edge
- VS Code
- Notepad
- Windows Terminal
- Maximized applications
- Full-screen applications
- Borderless games if possible

The note must not randomly appear above normal applications.

---

# 19. Taskbar Behavior

The application should ideally have one application/tray presence rather than one visible taskbar button per note.

Possible design:

```text
Taskbar
└── Desktop Notes
      ├── New Note
      ├── Notes
      ├── Settings
      └── Exit
```

A system-tray icon is optional for MVP but recommended later.

---

# 20. System Tray

Later implement:

Right-click tray icon:

```text
Desktop Notes
─────────────
New Note
Show All Notes
Hide All Notes
Settings
─────────────
Exit
```

Do not hide the application completely without providing a clear way to restore it.

---

# 21. Global Shortcut

Later implement:

```text
Ctrl + Alt + N
```

for:

```text
Create New Note
```

Do not add global shortcuts until the basic application is stable.

---

# 22. Auto Save

Auto-save should happen when:

- User changes text
- User moves note
- User resizes note
- User changes note settings

Use debouncing for text changes so the application does not write to disk on every keystroke.

For example:

```text
User types
   ↓
Wait ~500 ms after last change
   ↓
Save
```

Also save when the application closes.

---

# 23. Crash Safety

Never destroy the previous valid notes file before a new version has been successfully written.

Use an atomic-ish save strategy:

```text
notes.json
notes.tmp
      ↓
write temporary file
      ↓
success
      ↓
replace notes.json
```

If the application crashes during saving, the previous valid data should remain recoverable.

---

# 24. Data Location

Use the Windows user's local application data directory.

Example conceptual path:

```text
%LOCALAPPDATA%\DesktopNotes\
```

Store:

```text
DesktopNotes/
├── notes.json
├── settings.json
└── backups/
```

Do not store user data next to the executable.

---

# 25. Security

The application does not need elevated privileges.

Do not request:

- Administrator access
- UAC elevation
- Firewall permissions
- Network access

unless a future feature genuinely requires it.

Do not include telemetry.

---

# 26. Performance

The application should be lightweight.

Target:

- Very low idle CPU
- Low RAM usage
- No constant polling loops
- No unnecessary background services
- No WebView unless required
- No network activity

Avoid continuously polling the desktop hierarchy.

Prefer Windows events/hooks where practical.

---

# 27. Accessibility

At minimum:

- Keyboard focus should work.
- Text should be selectable.
- Text editor should support normal shortcuts.
- Font size should remain readable.
- Buttons should have accessible names.

---

# 28. Keyboard Shortcuts

Initial:

```text
Ctrl + S      Save
Ctrl + N      New Note
Ctrl + A      Select text
Ctrl + C      Copy
Ctrl + V      Paste
Ctrl + X      Cut
Ctrl + Z      Undo
Ctrl + Y      Redo
Delete        Delete selected text normally
```

Do not override normal text-editor behavior unnecessarily.

---

# 29. Settings

Later provide a small settings window.

Possible settings:

```text
General
├── Start with Windows
├── Default note color
├── Default font size
└── Auto-save interval

Behavior
├── Always on Top
├── Show in taskbar
└── Confirm before deleting

Appearance
├── Opacity
├── Rounded corners
└── Shadow
```

Do not implement settings until the core behavior works.

---

# 30. Color Options

Provide a few simple note colors:

```text
Yellow
Blue
Green
Pink
White
Gray
```

Do not build a complex color picker for the MVP.

---

# 31. Search

Search is optional.

If implemented:

```text
Ctrl + F
```

Search across all note text.

This is a later feature.

---

# 32. Rich Text

Do NOT implement rich text in the first version.

Start with plain text.

Later, if useful, add:

- Bold
- Italic
- Underline
- Font size
- Bullets

Plain text is more reliable and easier to persist.

---

# 33. Notifications

Do not add notifications in the MVP.

This is a notepad, not a task manager.

---

# 34. No Cloud Synchronization

Do not implement:

- Microsoft account
- Google account
- Cloud sync
- Online database

The first version must be completely local.

---

# 35. Installation

Eventually create a simple installer.

Preferred:

- MSIX or another standard Windows packaging mechanism
- Per-user installation if practical
- No administrator requirement if possible

The application should also be runnable directly during development.

---

# 36. Logging

Create lightweight local logging for development.

Log:

- Application startup
- Note creation/deletion
- Desktop-window attachment failures
- Shell/window hierarchy changes
- Save failures
- Restore failures

Do not log note contents.

Do not log sensitive user data.

---

# 37. Error Handling

If desktop integration fails:

Do not crash.

Fall back to a normal desktop window and show a clear development/debug message.

Example:

```text
Desktop integration could not be initialized.
The note is running in compatibility mode.
```

Do not silently pretend the required behavior is working.

---

# 38. Development Strategy

Build incrementally.

DO NOT attempt to create every feature in one giant generation.

Use this order:

## Phase 1 — Project

Create:

- Project
- Build configuration
- Basic application
- Basic window

Verify:

```text
dotnet build
```

works.

---

## Phase 2 — Basic Note

Implement:

- Note window
- Text editor
- Move
- Resize
- Close
- Create note

Verify manually.

---

## Phase 3 — Persistence

Implement:

- JSON storage
- Autosave
- Restore
- Multiple notes

Test by:

1. Create notes.
2. Close application.
3. Reopen.
4. Confirm everything returns.

---

## Phase 4 — Desktop Integration

This is the most technically important phase.

Research and implement the correct Windows Shell/desktop window integration.

Do not use Always-on-Top as a substitute.

Test:

```text
Normal app
    ↓
note underneath
    ↓
Win + D
    ↓
note visible
```

---

## Phase 5 — Multi-monitor/DPI

Implement and test:

- Multiple monitors
- DPI scaling
- Negative coordinates
- Monitor removal

---

## Phase 6 — Polish

Add:

- Better appearance
- Colors
- Context menu
- Tray icon
- Startup
- Settings

---

# 39. Testing Checklist

Create a checklist in the project README.

## Basic

- [ ] Application starts
- [ ] Create note
- [ ] Edit note
- [ ] Move note
- [ ] Resize note
- [ ] Delete note
- [ ] Multiple notes

## Persistence

- [ ] Text survives restart
- [ ] Position survives restart
- [ ] Size survives restart
- [ ] Color survives restart

## Desktop behavior

- [ ] Note is below normal applications
- [ ] Chrome covers note
- [ ] Explorer covers note
- [ ] VS Code covers note
- [ ] Win + D exposes note
- [ ] Win + D toggles correctly
- [ ] Note does not become topmost

## Display

- [ ] 100% DPI
- [ ] 125% DPI
- [ ] 150% DPI
- [ ] 200% DPI
- [ ] Multiple monitors
- [ ] Negative coordinates

## Reliability

- [ ] Explorer restart
- [ ] Application restart
- [ ] Corrupted JSON handled
- [ ] Save failure handled
- [ ] No admin privileges required

---

# 40. Important Technical Investigation

Before implementing the desktop integration, investigate the current Windows 10/11 behavior.

Do not assume old Windows 7 Gadget techniques are still correct.

Research:

- How `Win + D` works
- Windows Shell desktop windows
- `Progman`
- `WorkerW`
- Desktop window hierarchy
- Z-order
- `SetParent`
- `SetWindowLongPtr`
- `SetWindowPos`
- `EnumWindows`
- DWM
- Windows 10/11 virtual desktops
- Explorer restart behavior

Document the selected approach in:

```text
docs/desktop-integration.md
```

Include:

1. Technique selected
2. Why it works
3. Known limitations
4. Windows versions tested
5. Alternative techniques considered
6. Why those alternatives were rejected

---

# 41. Important Rule for AI Coding

You are allowed to modify the project architecture when necessary.

However:

- Do not blindly overwrite working code.
- Do not introduce unnecessary frameworks.
- Do not add dependencies without explaining why.
- Do not implement fake features.
- Do not claim a feature works without testing it.
- Do not replace desktop persistence with Always-on-Top.
- Do not delete working functionality when adding new functionality.

When a technical approach fails, explain why and try another approach.

---

# 42. Build Discipline

After every meaningful implementation:

```text
Build
 ↓
Run
 ↓
Test
 ↓
Fix
 ↓
Continue
```

Do not accumulate hundreds of untested changes.

After each phase, ensure the application still builds.

---

# 43. Git

Initialize a Git repository.

Use commits such as:

```text
Initial Windows desktop notes project
Add basic note window
Add note persistence
Add multiple notes
Add desktop shell integration
Fix Win+D behavior
Add multi-monitor support
Add startup support
Add settings
Polish note UI
```

Do not commit:

```text
bin/
obj/
.vs/
user-specific data
notes.json containing personal notes
secrets
```

---

# 44. README

Create a README containing:

- Project description
- Features
- Requirements
- Build instructions
- Run instructions
- Architecture
- Desktop integration explanation
- Known limitations
- Testing instructions
- Future features

---

# 45. Final MVP Definition

Do not consider the MVP complete until ALL of these work:

```text
1. Create note
2. Edit note
3. Move note
4. Resize note
5. Delete note
6. Multiple notes
7. Save automatically
8. Restore after restart
9. Notes remain visible after Win + D
10. Normal applications cover the notes
11. Notes are NOT always-on-top
12. Works on Windows 10/11
```

The most important acceptance test is:

```text
Create note
     ↓
Put note on desktop
     ↓
Open Chrome
     ↓
Chrome covers note
     ↓
Press Win + D
     ↓
Chrome disappears
     ↓
Note becomes visible
     ↓
Press Win + D
     ↓
Chrome returns
     ↓
Chrome covers note again
```

If this test fails, the desktop integration is not complete.

---

# 46. Future Features — Do Not Implement Initially

Possible future additions:

- Rich text
- Markdown
- Search
- Tags
- Pinning
- Note templates
- Global hotkeys
- System tray
- Themes
- Dark mode
- Opacity
- Note locking
- Password-protected notes
- Reminders
- Cloud synchronization
- Virtual desktop controls
- Import/export
- Backup/restore

Keep these out of the MVP.

---

# 47. First Task for Gemini

Start by inspecting the development environment.

Then:

1. Determine the available .NET SDK.
2. Determine whether Windows App SDK/WinUI 3 is available.
3. Create the project.
4. Build a minimal application.
5. Run it.
6. Create the first basic sticky-note window.
7. Do not implement desktop Shell integration yet.
8. Do not add unnecessary features.
9. After the basic note works, move to persistence.
10. Then investigate desktop integration.

Before making major architectural decisions, explain the decision briefly in the project documentation.

---

# 48. First Prompt to Give the Coding Agent

Use the following as the initial task:

> Read this entire specification before changing anything.
>
> We are building a Windows-only desktop persistent notepad application.
>
> The defining requirement is that notes behave like desktop widgets:
>
> - They remain visible when Windows executes Show Desktop (`Win + D`).
> - They remain underneath normal application windows.
> - They must NOT be implemented as Always-on-Top by default.
>
> First inspect the environment and determine the best supported C#/.NET Windows UI stack.
>
> Then create the minimal project and make sure it builds and runs.
>
> Implement only Phase 1 and Phase 2:
>
> 1. Project setup
> 2. Basic sticky-note window
> 3. Editable plain text
> 4. Move
> 5. Resize
> 6. Create/delete
>
> Do NOT implement desktop Shell integration yet.
>
> Do NOT implement cloud sync, rich text, reminders, or other future features.
>
> Keep the architecture simple and maintainable.
>
> After implementation, build and run the application and report:
>
> - Files created
> - Technology chosen
> - Build result
> - How to run it
> - Any issues encountered
> - What should be implemented next
>
> Do not claim success for anything that was not actually tested.

---

# 49. Second Prompt — Persistence

After Phase 1/2 works, give the agent:

> Implement Phase 3 from the specification.
>
> Add persistent local JSON storage and multiple independent notes.
>
> Persist:
>
> - ID
> - Text
> - Position
> - Size
> - Title
> - Color
> - Relevant settings
>
> Use the user's LocalAppData directory.
>
> Implement debounced autosave.
>
> Make saving crash-resistant.
>
> Restore all notes when the application starts.
>
> Test by creating several notes, closing the application, restarting it, and verifying their content, positions, and sizes.
>
> Do not implement desktop Shell integration yet.
>
> Build and test before reporting completion.

---

# 50. Third Prompt — Desktop Integration

Only after persistence works, give the agent:

> Now implement the most important feature: true desktop persistence.
>
> Read the `docs/desktop-integration.md` requirements first.
>
> Investigate the current Windows 10/11 desktop Shell/window hierarchy.
>
> The required behavior is:
>
> ```text
> Normal application windows
>         ↓
> Desktop notes
>         ↓
> Desktop/wallpaper
> ```
>
> When Chrome or another normal application overlaps a note, the application must cover the note.
>
> When Windows executes `Win + D` / Show Desktop, the note must remain visible.
>
> Do NOT solve this with Always-on-Top.
>
> Investigate the appropriate Win32/Shell approach and implement it carefully.
>
> Test repeatedly with:
>
> - Chrome
> - File Explorer
> - VS Code
> - Win + D
> - Maximized windows
> - Multiple notes
>
> If the chosen technique is fragile, document the limitation instead of hiding it.
>
> Build and run the application before reporting completion.

---

# 51. Fourth Prompt — Robustness

After desktop integration works:

> Improve the desktop persistent behavior without changing its intended stacking order.
>
> Add:
>
> - Multiple monitor support
> - DPI awareness
> - Monitor disconnect recovery
> - Explorer restart recovery where practical
> - Correct position restoration
>
> Do not turn the notes into Always-on-Top windows.
>
> Test all changes.
>
> Update the README and desktop integration documentation with actual tested behavior.

---

# 52. Fifth Prompt — Polish

Only after all core functionality is working:

> Polish the application UI without changing the core window behavior.
>
> Add:
>
> - Modern sticky-note appearance
> - Context menu
> - Note colors
> - Optional opacity
> - Optional Always-on-Top mode
> - Start with Windows
> - System tray
> - Settings
>
> Keep the default behavior as desktop-persistent and NOT Always-on-Top.
>
> Do not add unnecessary dependencies.
>
> Test every feature before declaring it complete.

---

# 53. Final Principle

The application is successful if it feels like this:

```text
                  WINDOWS APPLICATIONS

        Chrome     Explorer     VS Code
             \       |        /
              \      |       /
               \     |      /
                ───────────────
                  DESKTOP NOTE
                ───────────────
                   DESKTOP
                   WALLPAPER
```

The note is part of the desktop, not a floating application that happens to look like a note.

That distinction is the central technical requirement of this project.
