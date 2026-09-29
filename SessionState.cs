using System.IO;
using System.Text;

namespace Jester;

/// <summary>When and how big a file was on disk, to tell later whether it changed.</summary>
internal sealed record DiskStamp(long LastWriteUtcTicks, long Length)
{
    /// <summary>The file's stamp now, or null when it is missing or unreadable.</summary>
    public static DiskStamp? Of(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new DiskStamp(info.LastWriteTimeUtc.Ticks, info.Length) : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// One tab as the session keeps it. A saved file is only its path, read back from
/// disk on start. A tab with unsaved edits keeps its text, so nothing typed is lost
/// between runs, plus the stamp of the file it was edited against.
/// </summary>
internal sealed class SessionTab
{
    public string? Path { get; set; }

    /// <summary>The unsaved text, or null for a saved file that is read from disk.</summary>
    public string? Text { get; set; }

    public int Caret { get; set; }
    public int CodePage { get; set; } = 65001;
    public bool Bom { get; set; }
    public DiskStamp? Disk { get; set; }

    public bool IsDirty => Text is not null;
}

/// <summary>Every tab of one window, in order, and which one was active.</summary>
internal sealed class SessionSnapshot
{
    public DateTime SavedUtc { get; set; }
    public int ActiveTab { get; set; }
    public List<SessionTab> Tabs { get; set; } = new();
}

/// <summary>The rules for what the session keeps and how stored sessions come back.</summary>
internal static class SessionState
{
    /// <summary>What to keep of a tab, or null for a blank Untitled tab that holds nothing.</summary>
    public static SessionTab? Capture(string? path, string text, bool isDirty, int caret, Encoding encoding, DiskStamp? disk)
    {
        if (path is null && text.Length == 0)
            return null;

        var (codePage, bom) = Describe(encoding);
        bool keepText = isDirty || path is null;
        return new SessionTab
        {
            Path = path,
            Text = keepText ? text : null,
            Caret = keepText ? Math.Clamp(caret, 0, text.Length) : caret,
            CodePage = codePage,
            Bom = bom,
            Disk = path is null ? null : disk,
        };
    }

    /// <summary>A snapshot of the given tabs; the active index counts only the tabs kept.</summary>
    public static SessionSnapshot Snapshot(IReadOnlyList<SessionTab?> tabs, int selectedIndex, DateTime savedUtc)
    {
        var kept = new List<SessionTab>();
        int active = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (i == selectedIndex)
                active = kept.Count;
            if (tabs[i] is { } tab)
                kept.Add(tab);
        }
        return new SessionSnapshot
        {
            SavedUtc = savedUtc,
            ActiveTab = Math.Clamp(active, 0, Math.Max(0, kept.Count - 1)),
            Tabs = kept,
        };
    }

    /// <summary>
    /// Several stored sessions (windows that were closed, or crashed, while another was
    /// open) become one tab list: oldest first, each in its own order, and the newest
    /// session's active tab stays active. A saved file open in more than one comes back
    /// once. Two sets of unsaved edits to the same file both come back: the newest stays
    /// on the file, the older ones as Untitled tabs, so neither is lost.
    /// </summary>
    public static SessionSnapshot Merge(IEnumerable<SessionSnapshot> snapshots)
    {
        var ordered = snapshots.OrderBy(s => s.SavedUtc).ToList();
        var newest = ordered.Count > 0 ? ordered[^1] : null;

        // The newest unsaved edits for each file, which keep the file's path.
        var newestEdit = new Dictionary<string, SessionTab>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in ordered.SelectMany(s => s.Tabs))
            if (tab.Path is not null && tab.IsDirty)
                newestEdit[tab.Path] = tab;

        var merged = new SessionSnapshot { SavedUtc = newest?.SavedUtc ?? default };
        var byPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in ordered)
        {
            for (int i = 0; i < snapshot.Tabs.Count; i++)
            {
                var tab = snapshot.Tabs[i];
                int index = Add(tab);
                if (ReferenceEquals(snapshot, newest) && i == snapshot.ActiveTab)
                    merged.ActiveTab = index;
            }
        }
        return merged;

        int Add(SessionTab tab)
        {
            if (tab.Path is null)
            {
                merged.Tabs.Add(tab);
                return merged.Tabs.Count - 1;
            }

            if (tab.IsDirty && !ReferenceEquals(newestEdit[tab.Path], tab))
            {
                // Older edits to a file that has newer ones elsewhere: keep the words.
                merged.Tabs.Add(new SessionTab { Text = tab.Text, Caret = tab.Caret, CodePage = tab.CodePage, Bom = tab.Bom });
                return merged.Tabs.Count - 1;
            }

            if (byPath.TryGetValue(tab.Path, out int existing))
            {
                // Already listed; unsaved edits win over the saved copy.
                if (tab.IsDirty)
                    merged.Tabs[existing] = tab;
                return existing;
            }

            byPath[tab.Path] = merged.Tabs.Count;
            merged.Tabs.Add(tab);
            return merged.Tabs.Count - 1;
        }
    }

    /// <summary>
    /// Whether a file with unsaved edits was changed (or deleted) on disk after the edits
    /// began, so saving them would overwrite someone else's version.
    /// </summary>
    public static bool ChangedOnDisk(SessionTab tab, DiskStamp? now) =>
        tab.Path is not null && tab.IsDirty && tab.Disk != now;

    /// <summary>The tab to select once the restored tabs are open, given which of the
    /// stored tabs could be opened. A tab that failed passes the focus to the next one.</summary>
    public static int RestoredActive(IReadOnlyList<bool> opened, int active)
    {
        int count = opened.Count(o => o);
        if (count == 0)
            return 0;
        int before = opened.Take(Math.Clamp(active, 0, opened.Count)).Count(o => o);
        return Math.Min(before, count - 1);
    }

    /// <summary>An encoding as code page and byte-order mark, which is all a text editor
    /// needs to write the file the same way again.</summary>
    public static (int CodePage, bool Bom) Describe(Encoding encoding) =>
        (encoding.CodePage, encoding.GetPreamble().Length > 0);

    /// <summary>The encoding for a stored code page. An unknown one falls back to UTF-8.</summary>
    public static Encoding ToEncoding(int codePage, bool bom)
    {
        try
        {
            return codePage switch
            {
                65001 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom),
                1200 => new UnicodeEncoding(bigEndian: false, byteOrderMark: bom),
                1201 => new UnicodeEncoding(bigEndian: true, byteOrderMark: bom),
                12000 => new UTF32Encoding(bigEndian: false, byteOrderMark: bom),
                12001 => new UTF32Encoding(bigEndian: true, byteOrderMark: bom),
                _ => CodePage(codePage),
            };
        }
        catch
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        static Encoding CodePage(int codePage)
        {
            // .NET only ships UTF and ASCII encodings; the Windows code pages need this provider.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(codePage);
        }
    }
}
