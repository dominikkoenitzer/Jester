# Jester files

Jester keeps its own data in `%AppData%\Jester` (usually `C:\Users\<name>\AppData\Roaming\Jester`). The app itself is the single file `Jester.exe`, wherever the user keeps it.

## settings.json

Preferences, window bounds and the recent-files list, as indented JSON. Jester reads it when a window opens. It writes it under a second after a change to a Format or View choice, the font, the zoom, the recent files or the window's size, position or maximised state, and again when a window closes, so changes made to the file while Jester runs are overwritten. Close every Jester window before editing it.

Each write goes to a temporary `settings.json.<random>.tmp` beside it that then replaces the file, so a crash during a write leaves the previous settings whole.

Key names are case-sensitive and must match exactly. Unknown keys are ignored. A missing, empty or broken file makes Jester start with the defaults below and write a fresh file at the next change or on close.

```json
{
  "WindowLeft": null,
  "WindowTop": null,
  "WindowWidth": 950,
  "WindowHeight": 640,
  "WindowMaximized": false,
  "FontFamily": "Consolas",
  "FontSize": 11,
  "Bold": false,
  "Italic": false,
  "Zoom": 1,
  "WordWrap": false,
  "ShowLineNumbers": true,
  "AutoIndent": true,
  "StatusBarVisible": true,
  "RecentFiles": [],
  "OpenFiles": [],
  "ActiveTab": 0
}
```

| Key | Meaning |
| --- | --- |
| `WindowLeft`, `WindowTop` | Window position in device-independent pixels. `null` centres the window. A position off every screen is ignored. |
| `WindowWidth`, `WindowHeight` | Window size; at least 480 by 320. |
| `WindowMaximized` | Start maximised. |
| `FontFamily` | Name of an installed font family, used in every tab and in PDF export. |
| `FontSize` | Font size in points, 1 to 512. |
| `Bold`, `Italic` | Font style. |
| `Zoom` | 1 is 100%; kept between 0.2 and 5. |
| `WordWrap` | Format, Word Wrap. |
| `ShowLineNumbers` | View, Line Numbers. |
| `AutoIndent` | Format, Auto Indent: Enter repeats the leading spaces and tabs of the current line. |
| `StatusBarVisible` | View, Status Bar. |
| `RecentFiles` | Full paths for File, Open Recent, newest first, at most 10. |
| `OpenFiles`, `ActiveTab` | The saved files open at the last close. Jester reads them only when the `sessions` folder does not exist yet; after that the session files below decide which tabs open. |

To reset every preference, close Jester and delete `settings.json`.

## sessions\

One session per Jester window, so several windows never overwrite each other:

- `<id>.json`: the window's tabs, in order.
- `<id>.lock`: exists while that window runs. Windows deletes it when the window closes, also after a crash.
- `<id>.json.<random>.tmp`: present for a moment while a session is written.

A window writes its session about 2 seconds after the last change, at the latest about 12 seconds after the first one while typing goes on, when it closes, and when Windows shuts down. On start, a window takes over every session whose window is no longer running, opens their tabs (oldest session first), and deletes those files once its own session is written. A saved file open in several sessions comes back once. When two sessions hold unsaved edits to the same file, the newest stays on the file and the older edits come back as Untitled tabs.

Each `<id>.json` is compact JSON:

```json
{
  "SavedUtc": "2026-10-10T12:00:00Z",
  "ActiveTab": 0,
  "Tabs": [
    {
      "Path": "C:\\Notes\\todo.txt",
      "Text": null,
      "Caret": 120,
      "CodePage": 65001,
      "Bom": false,
      "Disk": { "LastWriteUtcTicks": 639000000000000000, "Length": 2048 },
      "IsDirty": false
    }
  ]
}
```

| Key | Meaning |
| --- | --- |
| `SavedUtc` | When the session was written. |
| `ActiveTab` | Index into `Tabs` of the selected tab. |
| `Path` | The tab's file, or `null` for an Untitled tab. |
| `Text` | The unsaved text, or `null` for a saved file, which is read from disk on start. |
| `Caret` | Caret position as a character offset. |
| `CodePage`, `Bom` | The tab's encoding: 65001 UTF-8, 1200 UTF-16 LE, 1201 UTF-16 BE, 12000 and 12001 UTF-32, any other number a Windows code page. `Bom` says whether a byte-order mark is written. |
| `Disk` | Time stamp and size of the file when the unsaved edits began, used to flag a file that changed on disk since. |
| `IsDirty` | Written for reference only; Jester derives it from `Text`. |

Empty Untitled tabs are not stored.

Never delete or edit a session while its `.lock` file exists. To read unsaved text without starting Jester, this PowerShell command lists every unsaved tab:

```powershell
Get-ChildItem "$env:APPDATA\Jester\sessions\*.json" | ForEach-Object { (Get-Content $_ -Raw | ConvertFrom-Json).Tabs | Where-Object { $null -ne $_.Text } | Select-Object Path, Text }
```

## crash.log

Jester appends an entry for every unexpected error and keeps running when it can:

```text
[2026-10-10T14:00:00.0000000+02:00] (Dispatcher)
<error type, message and stack trace>
```

`Dispatcher` marks an error in the window, which Jester survives after showing a message; `AppDomain` marks one that ended the process. Deleting the file is safe.
