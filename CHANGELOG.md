# Changelog

All notable changes to Jester are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-06-20

### Added
- **Tabbed documents** — open and edit many files at once, each with its own undo
  history, encoding, and scroll position. Close with the tab's ✕, middle-click, or
  `Ctrl+W`; switch with `Ctrl+Tab` / `Ctrl+Shift+Tab`.
- **Line-number gutter** with the current line highlighted, plus a current-line
  highlight in the editor. Toggle via **View → Line Numbers**.
- **Find in Files** (`Ctrl+Shift+F`) — search a whole folder (with optional
  subfolders and file filters); results show in a panel, double-click to jump.
- **Open Recent** menu — the last 10 opened files, persisted between sessions.
- **Session restore** — reopens the previous window size/position, open tabs, and
  preferences on launch.
- **Auto-indent** — new lines inherit the previous line's leading whitespace
  (**Format → Auto Indent**).
- **Line-ending** conversion (Windows/Unix/Macintosh) and **encoding** selection
  (UTF-8, UTF-8 BOM, UTF-16 LE/BE) under the **Format** menu.
- **Save All** (`Ctrl+Alt+S`) and **Open** now accepts multiple files at once.
- Crash guard: an unexpected error is logged and reported instead of closing the app.

### Changed
- `Ctrl+W` now closes the current tab; **Exit** moved to `Ctrl+Q`.

## [1.0.0] - 2026-06-20

### Added
- Core editing: New, Open, Save, Save As, unlimited undo/redo, cut/copy/paste, select all.
- **Export to PDF** — paginated A4 output with header and page numbers (`Ctrl+Shift+E`).
- Find & Replace with match case, wrap-around, and direction; Find Next/Previous (`F3` / `Shift+F3`).
- Go To Line, insert time/date, word wrap, font picker, zoom (menu and `Ctrl` + wheel).
- Status bar with character/line counts, caret position, zoom, line ending, and encoding.
- Drag-and-drop and command-line file opening.
- Custom purple & gold theme with a bespoke title bar and chrome across all windows.
- A purple & gold application icon.

### Safety
- Prompts to save unsaved changes before New, Open, drag-drop, close, and OS sign-out/shutdown.
- Atomic saves (write-to-temp then swap) so an interrupted write can't corrupt the file.
- BOM-aware encoding detection that preserves the original encoding and line endings.

[Unreleased]: https://github.com/dominikkoenitzer/Jester/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/dominikkoenitzer/Jester/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/dominikkoenitzer/Jester/releases/tag/v1.0.0
