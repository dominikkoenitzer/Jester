---
name: jester
description: Jester is a portable tabbed plain-text notepad for Windows that keeps unsaved tabs across restarts and crashes. Use this skill when the user asks how to do something in Jester, needs a shortcut or a setting, wants to recover unsaved text, or needs to install, update or remove it.
---

# Jester

Jester is a plain-text notepad for 64-bit Windows 10 and 11. It is one file, `Jester.exe`, with the .NET runtime bundled. It has no installer, no account, no sync, no update check and no plugins. It has no command-line flags and no scripting or automation interface, so help the user through its menus, its shortcuts and its files.

## What it does

- Opens many files at once in tabs. Each tab has its own undo history, encoding and line ending.
- Keeps every tab, saved or not, and brings it back on the next start, also after a crash or a Windows shutdown. Closing the window does not ask about unsaved tabs; it asks only when it cannot write them to disk. Closing a single tab with unsaved changes asks to save it.
- Saves through a temporary file that then replaces the original, so a failed save never leaves a half-written file.
- Finds and replaces in the current tab, and searches every file in a folder (Find in Files).
- Exports the current tab to an A4 PDF.
- Shows line numbers, highlights the current line, auto-indents and wraps words.
- Colours the syntax of TypeScript and JavaScript, Python, Rust, HTML, CSS, SCSS, Less, JSON, TOML, YAML, PowerShell and Markdown, chosen by the file's extension; fenced code blocks in Markdown are coloured in their own language. Other files, and new tabs until they are saved with one of those extensions, stay plain.
- Reads files with a byte-order mark as UTF-8, UTF-16 or UTF-32. A file without one is read as UTF-8 when it is valid UTF-8, otherwise in the Windows ANSI code page, and it is saved back the same way.

It has no language picker (the extension decides), no regular-expression or whole-word search, no spell check, and no printing (export a PDF and print that). It has a dark and a light theme, which follow the Windows app mode.

## Keyboard shortcuts

| Action | Keys |
| --- | --- |
| New tab | Ctrl+N |
| Open (several files at once) | Ctrl+O |
| Save | Ctrl+S |
| Save As | Ctrl+Shift+S |
| Save All | Ctrl+Alt+S |
| Export to PDF | Ctrl+Shift+E |
| Close tab | Ctrl+W, or middle-click the tab |
| Exit | Ctrl+Q |
| Next tab, previous tab | Ctrl+Tab, Ctrl+Shift+Tab |
| Undo, redo | Ctrl+Z, Ctrl+Y |
| Cut, copy, paste | Ctrl+X, Ctrl+C, Ctrl+V |
| Select all | Ctrl+A |
| Find | Ctrl+F |
| Find next, find previous | F3, Shift+F3 |
| Replace | Ctrl+H |
| Find in Files | Ctrl+Shift+F |
| Go to line | Ctrl+G |
| Insert time and date | F5 |
| Zoom in | Ctrl+Plus, or Ctrl+Plus on the number pad |
| Zoom out | Ctrl+Minus, or Ctrl+Minus on the number pad |
| Restore zoom to 100% | Ctrl+0, or Ctrl+0 on the number pad |
| Zoom with the mouse | Hold Ctrl and turn the mouse wheel |
| New line without auto-indent | Shift+Enter |
| Switch between insert and overtype | Insert |
| Open a Find in Files result | Enter or double-click on the result |
| Close the find panel or the Find in Files dialog | Escape |

The menus open with Alt+F (File), Alt+E (Edit), Alt+O (Format) and Alt+V (View). Word wrap, auto indent, font, line endings, encoding, line numbers and the status bar have no shortcut; they are in the Format and View menus.

## Menus

- File: New Tab, Open, Open Recent (last 10 files, plus Clear Recent Files), Reopen with Encoding (UTF-8, UTF-8 with BOM, UTF-16 LE, UTF-16 BE, Windows-1252, ANSI), Save, Save As, Save All, Export to PDF, Close Tab, Exit.
- Edit: Undo, Redo, Cut, Copy, Paste, Delete, Find, Find Next, Find Previous, Replace, Find in Files, Go To, Select All, Time/Date.
- Format: Word Wrap, Auto Indent, Font, Line Endings (Windows CRLF, Unix LF, Macintosh CR), Encoding (UTF-8, UTF-8 with BOM, UTF-16 LE, UTF-16 BE).
- View: Zoom (In, Out, Restore Default Zoom), Line Numbers, Syntax Highlighting, Syntax Colours (Pastel, Neutral), Status Bar.
- Right-click in the text: Cut, Copy, Paste, Delete, Select All.

## Details that answer common questions

- Find and Replace: a panel at the top right of the editor. Enter finds the next match, Shift+Enter the previous one; options are Match case and Wrap around. Replace All is one step that a single Undo takes back. F3 with no earlier search opens Find.
- Find in Files: enter the text, a folder (it starts at the folder of the current file) and filters separated by semicolons or commas, for example `*.txt;*.md` (default `*.*`). Options are Match case and Include subfolders. It searches plain text line by line, skips hidden and system files and files over 4 MB, and stops at 5,000 matches. Results appear in a panel above the status bar.
- Go To: a number past the last line goes to the last line.
- Time/Date inserts the current time and date in the short time and short date formats of the Windows region settings, time first, for example `2:05 PM 3/7/2026` for English (United States) or `14:05 07.03.2026` for German (Switzerland).
- Font and zoom apply to every tab. Font sizes from 1 to 512 points; zoom from 20% to 500% in steps of 10%.
- Changing the encoding in Format, Encoding marks the tab as unsaved; the file changes only when the user saves. File, Reopen with Encoding reads the file from disk again in the chosen encoding; it is available only for a tab with a file, and when the tab has unsaved changes it asks first, because reopening discards them. Converting line endings is an edit that Undo takes back. Enter always inserts the line ending the tab already uses.
- Tab names: an unsaved tab shows a dot after its name (screen readers hear a leading `*`). New tabs are called Untitled, Untitled 2 and so on. A restored tab whose file changed on disk since the unsaved edits shows `(changed on disk)`; saving it replaces the version on disk.
- Syntax colours: View, Syntax Highlighting turns them on and off for every tab; View, Syntax Colours picks the pastel set (the default) or a quieter neutral one, each in the dark and the light theme. A file with a line longer than 10,000 characters is shown plain to keep typing quick, and gets its colours back once no line is that long.
- The status bar shows characters, lines, line and column (plus the selection size), zoom, line ending and encoding.
- PDF export: A4 page, 2 cm margins, the file name as a heading, page numbers as "Page N of M" at the bottom, the editor's font at 6 to 24 points. Long lines wrap. Jester offers to open the PDF afterwards.

## Files on disk

Jester keeps everything in `%AppData%\Jester` (usually `C:\Users\<name>\AppData\Roaming\Jester`):

- `settings.json`: preferences, window size and position, recent files.
- `sessions\`: one `<id>.json` per window with its tabs, including unsaved text, and a `<id>.lock` while that window runs.
- `crash.log`: details of unexpected errors.

Formats and safe ways to change them are in [references/files.md](references/files.md).

## Command line

`Jester.exe [file ...]` opens each file in a tab next to the restored tabs, and the last one gets the focus. Arguments that start with `-` or `/` are ignored, and files that do not exist are skipped. Every start opens a new window. "Open with" in File Explorer works the same way.

## Install, update and remove

- Install: download `Jester.exe` from the Jester download page (`/download/windows`) and double-click it. If Windows says it protected your PC, click More info, then Run anyway. Keep the file in any folder; pin it to the taskbar or Start to find it again.
- Update: close every Jester window, download `Jester.exe` again and replace the old file. Settings and unsaved tabs stay in `%AppData%\Jester`. Jester does not update itself.
- Remove: close every Jester window and delete `Jester.exe`. To remove its data too, delete `%AppData%\Jester`. That folder holds unsaved text, so check `sessions\` first.

## How to help the user

1. For a how-to question, give the menu path and the shortcut from the tables above.
2. For a setting, point to the menu first. Edit `settings.json` only after every Jester window is closed, because each window writes the file a moment after every change to a preference, the recent files or its size and position, and again when it closes.
3. To reset preferences, close Jester and delete `%AppData%\Jester\settings.json`. Unsaved tabs in `sessions\` are not affected.
4. To recover unsaved text, start Jester; it reopens every tab. If Jester cannot start, read the `Text` field of the tabs in `%AppData%\Jester\sessions\*.json`. Never delete or edit session files while a Jester window is running.
5. If text looks garbled after opening, the file has no byte-order mark and is not UTF-8, so Jester read it in the ANSI code page shown in the status bar. Use File, Reopen with Encoding to read it again as UTF-8, UTF-8 with BOM, UTF-16 LE, UTF-16 BE, Windows-1252 or ANSI. Format, Encoding only sets how the tab is written on the next save.
6. After an error message, read `%AppData%\Jester\crash.log`.
7. Say plainly when Jester cannot do something (see the list at the top) instead of suggesting a setting that does not exist.
