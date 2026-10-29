using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Jester;

/// <summary>Opening, saving, exporting and the recent-files list.</summary>
public partial class MainWindow
{
    // ---------------------------------------------------------------- File ops

    private void OpenViaDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open",
            Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == true)
            foreach (string file in dialog.FileNames)
                OpenFile(file);
    }

    /// <summary>Opens a file in a tab (focusing an existing tab if already open), reusing
    /// a lone blank document. Safe to call from the command line, the Open dialog, drag
    /// &amp; drop, recent files, and search results.</summary>
    public void OpenFile(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch { full = path; }

        var existing = _docs.FirstOrDefault(d =>
            d.FilePath is not null && string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            Tabs.SelectedItem = existing;
            AddRecentFile(full);
            return;
        }

        string text;
        Encoding encoding;
        try
        {
            (text, encoding) = ReadFile(full);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, OpenFailure(full, ex),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DocumentTab target = _docs.Count == 1 && IsPristineEmpty(_docs[0])
            ? _docs[0]
            : CreateEmptyTab(select: false);

        LoadInto(target, text, full, encoding);
        Tabs.SelectedItem = target;
        AddRecentFile(full);
    }

    private static bool IsPristineEmpty(DocumentTab tab) =>
        tab.FilePath is null && !tab.IsDirty && tab.Editor.Document.TextLength == 0;

    private bool Save(DocumentTab tab) => tab.FilePath is null ? SaveAs(tab) : WriteToFile(tab, tab.FilePath);

    private bool SaveAs(DocumentTab tab)
    {
        var dialog = SaveAsDialog(tab);
        return dialog.ShowDialog(this) == true && WriteToFile(tab, dialog.FileName);
    }

    internal static SaveFileDialog SaveAsDialog(DocumentTab tab) => new()
    {
        Title = "Save As",
        Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
        FileName = tab.FilePath is null ? tab.UntitledName + ".txt" : Path.GetFileName(tab.FilePath),
        // Empty for a new tab, which leaves the dialog in the folder it last used.
        InitialDirectory = tab.FilePath is null ? "" : Path.GetDirectoryName(tab.FilePath) ?? "",
        DefaultExt = ".txt",
        AddExtension = true,
    };

    private void SaveAll()
    {
        foreach (var tab in _docs.ToList())
            if (tab.IsDirty && !Save(tab))
                break;
    }

    private bool WriteToFile(DocumentTab tab, string path)
    {
        try
        {
            List<DocumentTab> others;
            try
            {
                others = WriteTab(_docs, tab, path, FileOps.Real);
            }
            catch (EncoderFallbackException)
            {
                if (MessageBox.Show(this,
                        $"Some characters in {tab.Name} do not exist in {DescribeEncoding(tab.Encoding)}, so saving replaces them.\n\nSave it anyway?",
                        AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                    return false;
                others = WriteTab(_docs, tab, path, FileOps.Real, replaceUnmappable: true);
            }

            foreach (var other in others)
                CloseTab(other);
            AddRecentFile(path);
            if (ReferenceEquals(tab, Active))
                UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, SaveFailure(path, ex),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Writes the tab's text to the path and makes it the one tab on that file, as
    /// <see cref="OpenFile"/> expects. Another tab on the file is returned to be closed, or,
    /// when it has unsaved edits, keeps them as an untitled tab.</summary>
    internal static List<DocumentTab> WriteTab(IEnumerable<DocumentTab> docs, DocumentTab tab, string path, FileOps files,
        bool replaceUnmappable = false)
    {
        SafeWrite(path, tab.Editor.Text, tab.Encoding, files, replaceUnmappable);
        tab.FilePath = path;
        tab.Disk = DiskStamp.Of(path);
        tab.ChangedOnDisk = false;
        tab.IsDirty = false;

        string full = Path.GetFullPath(path);
        var others = docs.Where(d => !ReferenceEquals(d, tab) &&
            d.FilePath is not null && string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var other in others.Where(d => d.IsDirty))
        {
            other.FilePath = null;
            other.Disk = null;
            other.ChangedOnDisk = false;
        }
        return others.Where(d => !d.IsDirty).ToList();
    }

    /// <summary>Writes to a temp file in the same directory then atomically swaps it in,
    /// so an interrupted or failed write can never truncate the existing file. A code page
    /// that cannot hold a character throws <see cref="EncoderFallbackException"/> and leaves
    /// the file as it was, unless the caller allows the replacement.</summary>
    internal static void SafeWrite(string path, string text, Encoding encoding, FileOps files,
        bool replaceUnmappable = false) =>
        SafeWrite(path, temp => files.WriteText(temp, text, replaceUnmappable ? encoding : Strict(encoding)), files);

    /// <summary>Has <paramref name="write"/> fill a temp file in the same directory, then swaps
    /// it in, so a write that fails part way leaves the existing file as it was.</summary>
    internal static void SafeWrite(string path, Action<string> write, FileOps files)
    {
        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();
        string temp = Path.Combine(directory,
            "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        bool onlyCopy = false;
        try
        {
            write(temp);
            if (!File.Exists(full))
            {
                files.Move(temp, full, overwrite: false);
            }
            else
            {
                try
                {
                    files.Replace(temp, full);
                }
                catch when (!File.Exists(full) && File.Exists(temp))
                {
                    // ReplaceFile can fail after it took the file away (ERROR_UNABLE_TO_MOVE_REPLACEMENT).
                    onlyCopy = true;
                    files.Move(temp, full, overwrite: false);
                }
            }
        }
        finally
        {
            if (!onlyCopy && File.Exists(temp))
            {
                try { File.Delete(temp); }
                catch { /* best-effort cleanup of the temp file */ }
            }
        }
    }

    private static Encoding Strict(Encoding encoding)
    {
        // A UTF encoding holds every character; a code page would write "?" or a look-alike instead.
        if (encoding is UTF8Encoding or UnicodeEncoding or UTF32Encoding)
            return encoding;

        var strict = (Encoding)encoding.Clone();
        strict.EncoderFallback = EncoderFallback.ExceptionFallback;
        return strict;
    }

    /// <summary>Reads a text file, honouring a byte-order mark if present. With a UTF-8 mark or
    /// none, the bytes are UTF-8 only if they are valid UTF-8; otherwise they are read in the
    /// ANSI code page. The detected encoding is returned so the file round-trips faithfully.</summary>
    internal static (string Text, Encoding Encoding) ReadFile(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            // The reader would swap in a UTF-8 decoder that does not throw at a UTF-8 mark, so decode past the mark here.
            ReadOnlySpan<byte> mark = Encoding.UTF8.Preamble;
            if (bytes.AsSpan().StartsWith(mark))
                return (strictUtf8.GetString(bytes, mark.Length, bytes.Length - mark.Length), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            using var reader = new StreamReader(new MemoryStream(bytes), strictUtf8, detectEncodingFromByteOrderMarks: true);
            string text = reader.ReadToEnd();
            // Save with a UTF-8 encoder that does not throw, as before; the strict one is only for reading.
            Encoding encoding = ReferenceEquals(reader.CurrentEncoding, strictUtf8)
                ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                : reader.CurrentEncoding;
            return (text, encoding);
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8. Decoding it as UTF-8 anyway turned every invalid byte into U+FFFD,
            // and saving then wrote EF BF BD over the original character.
            Encoding ansi = AnsiEncoding();
            return (ansi.GetString(bytes), ansi);
        }
    }

    /// <summary>Reads a text file in the encoding the user chose rather than the detected one.</summary>
    internal static (string Text, Encoding Encoding) ReadFile(string path, Encoding encoding) =>
        (Decode(File.ReadAllBytes(path), encoding), encoding);

    /// <summary>Decodes the bytes, leaving out a byte-order mark that belongs to the encoding.</summary>
    internal static string Decode(byte[] bytes, Encoding encoding)
    {
        // Any UTF-8 drops the UTF-8 mark, so UTF-8 without one does not show it as a character.
        ReadOnlySpan<byte> bom = encoding is UTF8Encoding ? Encoding.UTF8.Preamble : encoding.Preamble;
        int skip = !bom.IsEmpty && bytes.AsSpan().StartsWith(bom) ? bom.Length : 0;
        return encoding.GetString(bytes, skip, bytes.Length - skip);
    }

    internal static Encoding ReopenEncoding(string kind) => kind switch
    {
        "utf-8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        "utf-16le" => Encoding.Unicode,
        "utf-16be" => Encoding.BigEndianUnicode,
        "windows-1252" => Windows1252(),
        "ansi" => AnsiEncoding(),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private static Encoding Windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private void FileMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        // Only a file on disk can be read again.
        ReopenMenu.IsEnabled = Active?.FilePath is not null;
        AnsiReopenMenuItem.Header = $"ANSI (Windows-{AnsiEncoding().CodePage})";
    }

    private void ReopenWithEncoding_Click(object sender, RoutedEventArgs e)
    {
        if (Active is not { FilePath: { } path } tab || sender is not MenuItem { Tag: string kind })
            return;

        if (tab.IsDirty && MessageBox.Show(this,
                $"Reopening {tab.Name} discards your unsaved changes to it.\n\nReopen it anyway?",
                AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        string text;
        Encoding encoding;
        try
        {
            (text, encoding) = ReadFile(path, ReopenEncoding(kind));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, OpenFailure(path, ex),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadInto(tab, text, path, encoding);
        ScheduleSessionSave();
        RefreshStatus();
        UpdateTitle();
        SyncFormatMenus();
    }

    private static Encoding AnsiEncoding()
    {
        // .NET only ships UTF and ASCII encodings; the Windows code pages need this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage); }
        catch { return Encoding.GetEncoding(1252); }
    }

    /// <summary>One plain sentence for a file that could not be read; the exception's own text stays out.</summary>
    internal static string OpenFailure(string path, Exception ex)
    {
        string name = DisplayName(path);
        // Reading a folder fails as access denied, which would send the user looking for a permission problem.
        if (Directory.Exists(path))
            return $"{name} is a folder, not a file.";

        return ex switch
        {
            FileNotFoundException or DirectoryNotFoundException => $"Could not find {name}.",
            UnauthorizedAccessException => $"You do not have permission to open {name}.",
            IOException io when IsInUse(io) => $"{name} is in use by another program.",
            _ => $"Could not open {name}.",
        };
    }

    internal static string SaveFailure(string path, Exception ex) =>
        WriteFailure(path, ex) ?? $"Could not save {DisplayName(path)}.";

    internal static string ExportFailure(string path, Exception ex) =>
        WriteFailure(path, ex) ?? $"Could not export {DisplayName(path)}.";

    private static string? WriteFailure(string path, Exception ex)
    {
        string name = DisplayName(path);
        return ex switch
        {
            DirectoryNotFoundException => $"The folder for {name} no longer exists.",
            UnauthorizedAccessException => $"You do not have permission to save {name} there.",
            IOException io when IsInUse(io) => $"{name} is in use by another program.",
            IOException io when (io.HResult & 0xFFFF) is 39 or 112 => $"There is not enough disk space to save {name}.",
            _ => null,
        };
    }

    // ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION.
    private static bool IsInUse(IOException ex) => (ex.HResult & 0xFFFF) is 32 or 33;

    private static string DisplayName(string path)
    {
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Length > 0 ? name : path;
    }

    /// <summary>Returns true if it is safe to discard the given document.</summary>
    private bool ConfirmClose(DocumentTab tab)
    {
        if (!tab.IsDirty)
            return true;

        Tabs.SelectedItem = tab;
        var result = MessageBox.Show(this, $"Do you want to save changes to {tab.Name}?",
            AppName, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => Save(tab),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private void ExportToPdf()
    {
        if (Active is not { } tab)
            return;

        string suggested = (tab.FilePath is null
            ? tab.UntitledName
            : Path.GetFileNameWithoutExtension(tab.FilePath)) + ".pdf";

        var dialog = new SaveFileDialog
        {
            Title = "Export to PDF",
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = suggested,
            DefaultExt = ".pdf",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            string title = tab.FilePath is null ? tab.UntitledName : Path.GetFileName(tab.FilePath);
            PdfExporter.Export(dialog.FileName, tab.Editor.Text, title, _fontFamily.Source, _baseFontSizePoints);

            var open = MessageBox.Show(this, "PDF exported successfully.\n\nOpen it now?",
                AppName, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ExportFailure(dialog.FileName, ex),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------------------------------------ Recent files

    private void AddRecentFile(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return; }

        _recentFiles.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, full);
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);

        RebuildRecentMenu();
        ScheduleSettingsSave();
    }

    private void RebuildRecentMenu()
    {
        RecentFilesMenu.Items.Clear();

        if (_recentFiles.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem { Header = "(no recent files)", IsEnabled = false });
            return;
        }

        int n = 1;
        foreach (string path in _recentFiles)
        {
            var item = new MenuItem { Header = $"_{n} {Path.GetFileName(path)}", ToolTip = path, Tag = path };
            item.Click += RecentFile_Click;
            RecentFilesMenu.Items.Add(item);
            n++;
        }

        RecentFilesMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "_Clear Recent Files" };
        clear.Click += (_, _) => { _recentFiles.Clear(); RebuildRecentMenu(); ScheduleSettingsSave(); };
        RecentFilesMenu.Items.Add(clear);
    }

    private void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
            return;

        if (File.Exists(path))
        {
            OpenFile(path);
        }
        else
        {
            MessageBox.Show(this, $"The file no longer exists:\n\n{path}",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            _recentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            RebuildRecentMenu();
            ScheduleSettingsSave();
        }
    }
}
