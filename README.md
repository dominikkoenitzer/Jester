<div align="center">

<img src="Assets/logo.png" width="112" alt="Jester logo" />

# Jester

**A lightweight, beautiful notepad for Windows — purple & gold.**

[![CI](https://github.com/dominikkoenitzer/Jester/actions/workflows/ci.yml/badge.svg)](https://github.com/dominikkoenitzer/Jester/actions/workflows/ci.yml)
[![Download](https://img.shields.io/badge/download-Jester.exe-E8B53D)](../../releases/latest)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)

[**⬇ Download**](#-download) · [Features](#-features) · [Shortcuts](#-keyboard-shortcuts) · [Build](#-build-from-source) · [Contributing](#-contributing)

<img src="screenshot.png" width="760" alt="Jester screenshot" />

</div>

## ⬇ Download

1. Head to the [**latest release**](../../releases/latest).
2. Download **`Jester.exe`**.
3. Double-click to run. That's it.

It's a single, **portable** file — no installer, and **no .NET install required** (the runtime is bundled). Works on **Windows 10 and 11 (64-bit)**.

> First launch may show a SmartScreen prompt because the build is unsigned — choose *More info → Run anyway*.

## ✨ Features

- **Tabbed editing** — work on many files at once, each with its own undo history and
  encoding. Close with `Ctrl+W` or middle-click; switch with `Ctrl+Tab`.
- **Line numbers** — a gutter with the current line highlighted, plus a current-line
  highlight in the editor (toggle in **View**).
- **Find in Files** — search a whole folder (`Ctrl+Shift+F`) with filters and subfolders;
  double-click a result to jump straight to it.
- **Files** — New, Open (multiple at once), Save, Save As, Save All, **Open Recent**,
  drag-and-drop, and "Open with Jester" from Explorer.
- **Session restore** — reopens your window, tabs, and preferences next launch.
- **Export to PDF** — turn any note into a clean, paginated A4 PDF (`Ctrl+Shift+E`).
- **Find & Replace** — find, replace, replace all, match case, wrap-around, and direction.
- **Go To Line**, **insert time/date**, **auto-indent**, unlimited **undo/redo**.
- **Format** — word wrap, a font picker, and line-ending / encoding conversion.
- **View** — zoom (menu or `Ctrl` + mouse wheel) and a toggleable status bar.
- **Status bar** — characters, lines, caret position, zoom, line ending, and encoding.
- **Safe by default** — prompts before discarding unsaved work (including on sign-out/shutdown) and saves atomically so a crash can't corrupt your file.
- **Encoding aware** — detects UTF-8/UTF-16 BOMs and preserves the file's original encoding and line endings.

## ⌨ Keyboard shortcuts

| Action | Shortcut | Action | Shortcut |
| --- | --- | --- | --- |
| New Tab | `Ctrl+N` | Find | `Ctrl+F` |
| Open | `Ctrl+O` | Find Next / Previous | `F3` / `Shift+F3` |
| Save | `Ctrl+S` | Replace | `Ctrl+H` |
| Save As | `Ctrl+Shift+S` | Find in Files | `Ctrl+Shift+F` |
| Save All | `Ctrl+Alt+S` | Go To Line | `Ctrl+G` |
| Close Tab | `Ctrl+W` | Time/Date | `F5` |
| Next / Prev Tab | `Ctrl+Tab` / `Ctrl+Shift+Tab` | Zoom In / Out | `Ctrl++` / `Ctrl+-` |
| Export to PDF | `Ctrl+Shift+E` | Restore Zoom | `Ctrl+0` |
| Exit | `Ctrl+Q` | Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Select All | `Ctrl+A` | | |

## 🛠 Build from source

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download) on Windows.

```sh
git clone https://github.com/dominikkoenitzer/Jester.git
cd Jester
dotnet run -c Release
```

Produce the portable single-file build that ships in releases:

```sh
dotnet publish Jester.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

The executable lands in `bin/Release/net9.0-windows/win-x64/publish/Jester.exe`.

## 🧱 Project structure

| Path | Purpose |
| --- | --- |
| `App.xaml(.cs)` | Application entry point, session-end and crash handling. |
| `MainWindow.xaml(.cs)` | Main window: menus, tabs, status bar, and command logic. |
| `EditorView.cs` | One document's editor surface (text box + gutter + current-line highlight). |
| `LineNumberMargin.cs` | The line-number gutter drawn beside each editor. |
| `DocumentTab.cs` | Per-tab document state (path, encoding, dirty flag). |
| `AppSettings.cs` | Loads/saves preferences and the last session as JSON. |
| `ThemedWindow.cs` | Base window with the custom title bar / chrome. |
| `Theme.xaml` | The purple & gold theme — palette and control styles. |
| `JesterCommands.cs` | Custom routed commands and key gestures. |
| `PdfExporter.cs` | Renders the document to PDF (QuestPDF). |
| `FindReplaceWindow`, `FindInFilesWindow`, `GoToWindow`, `FontWindow` | Dialogs. |
| `Assets/jester.ico` | Application icon. |

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md). Found a bug or have an idea? [Open an issue](../../issues/new/choose).

## 📄 License

Jester is free software licensed under the **[GNU GPL v3.0](LICENSE)**.
Copyright © 2026 Dominik Könitzer.

<div align="center"><sub>Built with C# and WPF on .NET 9.</sub></div>
