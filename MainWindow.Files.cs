using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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
            MessageBox.Show(this, $"Could not open the file.\n\n{ex.Message}",
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
        tab.FilePath is null && !tab.IsDirty && tab.Editor.Text.Length == 0;

    private bool Save(DocumentTab tab) => tab.FilePath is null ? SaveAs(tab) : WriteToFile(tab, tab.FilePath);

    private bool SaveAs(DocumentTab tab)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save As",
            Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = tab.FilePath is null ? tab.UntitledName + ".txt" : Path.GetFileName(tab.FilePath),
            DefaultExt = ".txt",
            AddExtension = true,
        };

        return dialog.ShowDialog(this) == true && WriteToFile(tab, dialog.FileName);
    }

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
            SafeWrite(path, tab.Editor.Text, tab.Encoding);
            tab.FilePath = path;
            tab.IsDirty = false;
            AddRecentFile(path);
            if (ReferenceEquals(tab, Active))
                UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the file.\n\n{ex.Message}",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Writes to a temp file in the same directory then atomically swaps it in,
    /// so an interrupted or failed write can never truncate the existing file.</summary>
    private static void SafeWrite(string path, string text, Encoding encoding)
    {
        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();
        string temp = Path.Combine(directory,
            "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.WriteAllText(temp, text, encoding);
            if (File.Exists(full))
                File.Replace(temp, full, destinationBackupFileName: null);
            else
                File.Move(temp, full);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); }
                catch { /* best-effort cleanup of the temp file */ }
            }
        }
    }

    /// <summary>Reads a text file, honouring a byte-order mark if present and defaulting to
    /// UTF-8 otherwise, returning the detected encoding so the file round-trips faithfully.</summary>
    private static (string Text, Encoding Encoding) ReadFile(string path)
    {
        using var reader = new StreamReader(path,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        return (text, reader.CurrentEncoding);
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
            MessageBox.Show(this, $"Could not export the PDF.\n\n{ex.Message}",
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
        clear.Click += (_, _) => { _recentFiles.Clear(); RebuildRecentMenu(); };
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
        }
    }
}
