# PermaNotes

**Always-on-desktop sticky notes for Windows and macOS** — lightweight, offline, native performance, and built to stay out of your way.

PermaNotes pins rich-text sticky notes directly to your desktop layer, so they're always visible (even alongside your wallpaper icons, shown on Win+D or macOS Show Desktop) without cluttering your taskbar or stealing focus from your open applications.

Re-engineered from the ground up using **Avalonia UI** and **.NET 8** with native Win32 and AppKit/CoreGraphics interop for 100% free, MIT-compliant cross-platform support.

---

## ✨ Features

- **Always-on-Desktop Pinning**:
  - **Windows**: Notes sit behind active application windows and survive `Win + D` via Win32 `WorkerW` / `Progman` reparenting.
  - **macOS**: Configured at `kCGDesktopWindowLevel` with `CanJoinAllSpaces | Stationary | IgnoresCycle` to survive Mission Control and F11 Show Desktop gestures.
- **Region-Exempt Click-Through**:
  - Notes can be set to click-through mode so clicks pass through to background windows/desktop icons.
  - The top 34px drag header and action buttons remain interactive via `WM_NCHITTEST` subclassing on Windows and AppKit `-[NSView hitTest:]` method swizzling on macOS.
- **Rich Text Editing**:
  - Full-featured formatting powered by `AvRichTextBox` (Bold, Italic, Underline, Strikethrough, Highlight, Font Sizes, Lists).
  - Compatible with standard RTF documents and existing note archives.
- **Customizable Aesthetics**:
  - Preset palette and custom RGB/HSV Color Wheel Picker.
  - Adjustable opacity and acrylic blur support.
  - Dynamic adaptive contrast that automatically switches icons and text between light and dark themes.
- **Note Management & Search**:
  - Centralized **Note Manager** dashboard with instant title/body search, note activation, duplication, and deletion.
  - System Tray integration with quick note creation and window management.
- **100% Offline & Private**:
  - Atomic JSON and per-note RTF file persistence with debounced auto-saving.
  - Zero telemetry, zero cloud dependencies, no administrator privileges required.

---

## 🏛 Architecture

```
PermaNotes.sln
├── src/
│   ├── PermaNotes.Core/             # .NET 8 — 100% shared Models, Storage, and Native Service Interfaces
│   ├── PermaNotes.UI/               # Avalonia UI — MVVM Views, ViewModels, and Dialogs
│   ├── PermaNotes.Platform.Windows/ # Win32 Native Layer (WorkerW Desktop Pin, WM_NCHITTEST, HKCU Run)
│   ├── PermaNotes.Platform.MacOS/   # macOS Native Layer (AppKit/CoreGraphics P/Invoke, LaunchAgent)
│   ├── PermaNotes.Desktop/          # Desktop Entry Point & Dependency Injection (Program.cs)
│   └── AvRichTextBox/               # Embedded MIT Rich Text Control library
└── tests/
    └── PermaNotes.Core.Tests/       # Unit tests for storage, atomic persistence, and models
```

---

## 🚀 Build & Run

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Development Run

```bash
# Run on Windows or macOS
dotnet run --project src/PermaNotes.Desktop/PermaNotes.Desktop.csproj
```

### Run Tests

```bash
dotnet test tests/PermaNotes.Core.Tests/PermaNotes.Core.Tests.csproj
```

### Publishing Standalone Releases

#### Windows (Single-File Self-Contained)
```powershell
dotnet publish src/PermaNotes.Desktop/PermaNotes.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/windows
```
Produces `dist/windows/PermaNotes.Desktop.exe` with no external .NET installation required.

#### macOS (Self-Contained)
```bash
# Apple Silicon (M1/M2/M3/M4)
dotnet publish src/PermaNotes.Desktop/PermaNotes.Desktop.csproj -c Release -r osx-arm64 --self-contained -o dist/macos-arm64

# Intel Mac
dotnet publish src/PermaNotes.Desktop/PermaNotes.Desktop.csproj -c Release -r osx-x64 --self-contained -o dist/macos-x64
```

Packaging into an `.app` bundle / `.dmg` can be completed on macOS or in GitHub Actions via `Avalonia.Parcel`:
```bash
dotnet tool install -g Avalonia.Parcel
parcel package --input dist/macos-arm64 --format dmg --output dist/macos-dmg
```

---

## 🛠 CI/CD Pipeline

Automated builds and tests are managed via GitHub Actions (`.github/workflows/build-avalonia.yml`):
- **Windows Build**: Restores, builds, tests, and publishes self-contained single-file Windows artifacts.
- **macOS Build**: Restores, compiles for `osx-arm64` and `osx-x64` on `macos-latest` runners, and packages unsigned `.app` / `.dmg` bundles.

---

## 📄 License

MIT License — see LICENSE for details.
