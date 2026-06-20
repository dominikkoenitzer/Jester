using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Jester;

public partial class MainWindow : ThemedWindow
{
    private const string AppName = "Jester";
    private const double PointsToDip = 96.0 / 72.0;
    private const int MaxRecentFiles = 10;
    private const int MaxFindResults = 5000;

    private readonly ObservableCollection<DocumentTab> _docs = new();
    private readonly AppSettings _settings;
    private readonly List<string> _recentFiles = new();

    private bool _isLoadingFile;
    private DocumentTab? _lastActive;

    // Global formatting, shared by every tab so the editor feels like one app.
    private FontFamily _fontFamily = new("Consolas");
    private double _baseFontSizePoints = 11;
    private double _zoom = 1.0;
    private bool _bold;
    private bool _italic;
    private bool _wordWrap;
    private bool _showLineNumbers = true;
    private bool _autoIndent = true;

    // Last-used search settings, shared between the Find/Replace dialog and F3.
    private string _searchText = "";
    private bool _matchCase;
    private bool _wrapAround = true;

    private FindReplaceWindow? _findWindow;
    private FindInFilesWindow? _findInFilesWindow;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        Tabs.ItemsSource = _docs;

        RegisterCommands();
        ApplySettingsToGlobals();
        RestoreWindowBounds();
        RestoreSession();

        Loaded += (_, _) => Active?.Editor.Focus();
    }

    private DocumentTab? Active => Tabs.SelectedItem as DocumentTab;
    private TextBox? ActiveEditor => Active?.Editor;

    // ------------------------------------------------------------- Commands

    private void RegisterCommands()
    {
        void Bind(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler? canExecute = null)
            => CommandBindings.Add(new CommandBinding(command, handler, canExecute));

        Bind(ApplicationCommands.New, (_, e) => { e.Handled = true; NewFile(); });
        Bind(ApplicationCommands.Open, (_, e) => { e.Handled = true; OpenViaDialog(); });
        Bind(ApplicationCommands.Save, (_, e) => { e.Handled = true; if (Active is { } t) Save(t); });
        Bind(JesterCommands.SaveAs, (_, e) => { e.Handled = true; if (Active is { } t) SaveAs(t); });
        Bind(JesterCommands.SaveAll, (_, e) => { e.Handled = true; SaveAll(); });
        Bind(JesterCommands.CloseTab, (_, e) => { e.Handled = true; if (Active is { } t) CloseTab(t); });
        Bind(JesterCommands.Exit, (_, e) => { e.Handled = true; Close(); });

        Bind(JesterCommands.NextTab, (_, e) => { e.Handled = true; CycleTab(+1); });
        Bind(JesterCommands.PreviousTab, (_, e) => { e.Handled = true; CycleTab(-1); });

        Bind(ApplicationCommands.Find, (_, e) => { e.Handled = true; ShowFindReplace(replaceMode: false); });
        Bind(ApplicationCommands.Replace, (_, e) => { e.Handled = true; ShowFindReplace(replaceMode: true); });
        Bind(JesterCommands.FindNext, (_, e) => { e.Handled = true; FindNextFromMenu(searchDown: true); });
        Bind(JesterCommands.FindPrevious, (_, e) => { e.Handled = true; FindNextFromMenu(searchDown: false); });
        Bind(JesterCommands.FindInFiles, (_, e) => { e.Handled = true; ShowFindInFiles(); });
        Bind(JesterCommands.GoTo, (_, e) => { e.Handled = true; ShowGoTo(); });
        Bind(JesterCommands.InsertDateTime, (_, e) => { e.Handled = true; InsertDateTime(); });

        Bind(JesterCommands.ExportPdf, (_, e) => { e.Handled = true; ExportToPdf(); });
        Bind(JesterCommands.ChooseFont, (_, e) => { e.Handled = true; ChooseFont(); });
        Bind(JesterCommands.ZoomIn, (_, e) => { e.Handled = true; StepZoom(+0.1); });
        Bind(JesterCommands.ZoomOut, (_, e) => { e.Handled = true; StepZoom(-0.1); });
        Bind(JesterCommands.RestoreZoom, (_, e) => { e.Handled = true; ApplyZoom(1.0); });

        // Editing commands are routed to the active tab's editor, so they work both
        // from the menu (which steals focus) and via keyboard while editing.
        Bind(ApplicationCommands.Undo, (_, e) => { e.Handled = true; ActiveEditor?.Undo(); },
            (_, e) => e.CanExecute = ActiveEditor?.CanUndo == true);
        Bind(ApplicationCommands.Redo, (_, e) => { e.Handled = true; ActiveEditor?.Redo(); },
            (_, e) => e.CanExecute = ActiveEditor?.CanRedo == true);
        Bind(ApplicationCommands.Cut, (_, e) => { e.Handled = true; ActiveEditor?.Cut(); },
            (_, e) => e.CanExecute = ActiveEditor?.SelectionLength > 0);
        Bind(ApplicationCommands.Copy, (_, e) => { e.Handled = true; ActiveEditor?.Copy(); },
            (_, e) => e.CanExecute = ActiveEditor?.SelectionLength > 0);
        Bind(ApplicationCommands.Paste, (_, e) => { e.Handled = true; ActiveEditor?.Paste(); },
            (_, e) => e.CanExecute = ActiveEditor is not null && Clipboard.ContainsText());
        Bind(ApplicationCommands.Delete, (_, e) => { e.Handled = true; DeleteSelectionOrChar(); },
            (_, e) => e.CanExecute = ActiveEditor is { } ed && (ed.SelectionLength > 0 || ed.CaretIndex < ed.Text.Length));
        Bind(ApplicationCommands.SelectAll, (_, e) => { e.Handled = true; ActiveEditor?.SelectAll(); },
            (_, e) => e.CanExecute = ActiveEditor?.Text.Length > 0);
    }

    private void DeleteSelectionOrChar()
    {
        if (ActiveEditor is not { } ed)
            return;

        if (ed.SelectionLength > 0)
        {
            ed.SelectedText = "";
        }
        else if (ed.CaretIndex < ed.Text.Length)
        {
            int caret = ed.CaretIndex;
            ed.Text = ed.Text.Remove(caret, 1);
            ed.CaretIndex = caret;
        }
    }

    // --------------------------------------------------------- Tab lifecycle

    private DocumentTab CreateEmptyTab(bool select)
    {
        var tab = new DocumentTab();
        ApplyFormattingTo(tab);

        var ed = tab.Editor;
        ed.TextChanged += (_, _) => OnEditorTextChanged(tab);
        ed.SelectionChanged += (_, _) => { if (ReferenceEquals(tab, Active)) UpdatePositionInfo(); };
        ed.PreviewKeyDown += (_, e) => Editor_PreviewKeyDown(tab, e);
        ed.PreviewMouseWheel += Editor_PreviewMouseWheel;
        ed.PreviewDragOver += Editor_PreviewDragOver;
        ed.PreviewDrop += Editor_PreviewDrop;

        tab.View.SetTotalLines(1);
        _docs.Add(tab);
        if (select)
            Tabs.SelectedItem = tab;
        return tab;
    }

    private void LoadInto(DocumentTab tab, string text, string? path, Encoding encoding)
    {
        _isLoadingFile = true;
        tab.Editor.Text = text;
        tab.Editor.CaretIndex = 0;
        tab.Editor.ScrollToHome();
        _isLoadingFile = false;

        tab.FilePath = path;
        tab.Encoding = encoding;
        tab.IsDirty = false;
        tab.View.SetTotalLines(GetLogicalLineCount(text));
    }

    private void NewFile()
    {
        CreateEmptyTab(select: true);
        Active?.Editor.Focus();
    }

    private void CycleTab(int direction)
    {
        if (_docs.Count < 2)
            return;
        int next = (Tabs.SelectedIndex + direction + _docs.Count) % _docs.Count;
        Tabs.SelectedIndex = next;
    }

    private void CloseTab(DocumentTab tab)
    {
        // Remember what the user was actually working on; ConfirmClose may briefly
        // select `tab` to show its save prompt.
        var previouslyActive = Active;

        if (!ConfirmClose(tab))
            return;

        int index = _docs.IndexOf(tab);
        if (ReferenceEquals(_lastActive, tab))
            _lastActive = null;
        _docs.Remove(tab);

        if (_docs.Count == 0)
        {
            CreateEmptyTab(select: true);
        }
        else if (previouslyActive is not null && !ReferenceEquals(previouslyActive, tab) && _docs.Contains(previouslyActive))
        {
            // Closing a background tab must not move the user off their active document.
            Tabs.SelectedItem = previouslyActive;
        }
        else
        {
            Tabs.SelectedIndex = Math.Min(index, _docs.Count - 1);
        }
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DocumentTab tab })
            CloseTab(tab);
    }

    private void Tabs_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;
        if (FindAncestor<TabItem>(e.OriginalSource as DependencyObject)?.DataContext is DocumentTab tab)
        {
            e.Handled = true;
            CloseTab(tab);
        }
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Active is null || ReferenceEquals(Active, _lastActive))
            return;
        _lastActive = Active;

        RefreshStatus();
        UpdateTitle();
        SyncFormatMenus();

        var ed = Active.Editor;
        Dispatcher.BeginInvoke(new Action(() => ed.Focus()), DispatcherPriority.Input);
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var node = start; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is T match)
                return match;
        return null;
    }

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

    // ------------------------------------------------------------- Edit extras

    private void InsertDateTime()
    {
        if (ActiveEditor is not { } ed)
            return;

        string stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
        int caret = ed.SelectionStart;
        ed.SelectedText = stamp;
        ed.CaretIndex = caret + stamp.Length;
        ed.Focus();
    }

    // --------------------------------------------------------------- Find APIs
    // Called by FindReplaceWindow; also reused by the F3 / Shift+F3 menu commands.

    public bool FindNext(string search, bool matchCase, bool wrapAround, bool searchDown)
    {
        _searchText = search;
        _matchCase = matchCase;
        _wrapAround = wrapAround;
        return DoFind(searchDown);
    }

    public bool ReplaceNext(string search, string replace, bool matchCase, bool wrapAround, bool searchDown)
    {
        _searchText = search;
        _matchCase = matchCase;
        _wrapAround = wrapAround;

        if (ActiveEditor is not { } ed)
            return false;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (ed.SelectionLength > 0 && string.Equals(ed.SelectedText, search, comparison))
        {
            int start = ed.SelectionStart;
            ed.SelectedText = replace;
            ed.Select(start, replace.Length);
        }

        return DoFind(searchDown);
    }

    public int ReplaceAll(string search, string replace, bool matchCase)
    {
        if (string.IsNullOrEmpty(search) || ActiveEditor is not { } ed)
            return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        string text = ed.Text;
        var builder = new StringBuilder(text.Length);
        int index = 0, count = 0;

        while (true)
        {
            int found = text.IndexOf(search, index, comparison);
            if (found < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, found - index);
            builder.Append(replace);
            index = found + search.Length;
            count++;
        }

        if (count > 0)
        {
            int caret = ed.CaretIndex;
            ed.Text = builder.ToString();
            ed.CaretIndex = Math.Min(caret, ed.Text.Length);
        }

        return count;
    }

    private bool DoFind(bool searchDown)
    {
        if (string.IsNullOrEmpty(_searchText) || ActiveEditor is not { } ed)
            return false;

        string text = ed.Text;
        if (text.Length == 0)
            return false;

        var comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int index;

        if (searchDown)
        {
            int start = Math.Min(ed.SelectionStart + ed.SelectionLength, text.Length);
            index = text.IndexOf(_searchText, start, comparison);
            if (index < 0 && _wrapAround)
                index = text.IndexOf(_searchText, 0, comparison);
        }
        else
        {
            int start = ed.SelectionStart - 1;
            index = start >= 0 ? text.LastIndexOf(_searchText, start, comparison) : -1;
            if (index < 0 && _wrapAround)
                index = text.LastIndexOf(_searchText, text.Length - 1, comparison);
        }

        if (index < 0)
            return false;

        ed.Select(index, _searchText.Length);
        ScrollSelectionIntoView(index);
        return true;
    }

    private void ScrollSelectionIntoView(int charIndex)
    {
        if (ActiveEditor is not { } ed)
            return;
        try
        {
            int line = ed.GetLineIndexFromCharacterIndex(charIndex);
            if (line >= 0)
                ed.ScrollToLine(line);
        }
        catch
        {
            // Layout not ready; the selection is still set, just not scrolled to.
        }
    }

    private void FindNextFromMenu(bool searchDown)
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            ShowFindReplace(replaceMode: false);
            return;
        }

        if (!DoFind(searchDown))
            ReportNotFound(_searchText);
    }

    private void ReportNotFound(string search) =>
        MessageBox.Show(this, $"Cannot find \"{search}\".",
            AppName, MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowFindReplace(bool replaceMode)
    {
        if (ActiveEditor is not { } ed)
            return;

        if (_findWindow is null || !_findWindow.IsLoaded)
        {
            _findWindow = new FindReplaceWindow(this) { Owner = this };
            _findWindow.Closed += (_, _) => _findWindow = null;
        }

        string selected = ed.SelectedText;
        if (ed.SelectionLength > 0 && !selected.Contains('\n') && !selected.Contains('\r'))
            _findWindow.SearchText = selected;
        else if (_searchText.Length > 0)
            _findWindow.SearchText = _searchText;

        _findWindow.ShowReplace(replaceMode);
        _findWindow.MatchCase = _matchCase;
        _findWindow.WrapAround = _wrapAround;
        _findWindow.Show();
        _findWindow.Activate();
        _findWindow.FocusSearchBox();
    }

    // ----------------------------------------------------------- Find in Files

    private void ShowFindInFiles()
    {
        string? folder = Active?.FilePath is { } path ? Path.GetDirectoryName(path) : null;
        string? seed = ActiveEditor is { SelectionLength: > 0 } ed && !ed.SelectedText.Contains('\n')
            ? ed.SelectedText
            : null;

        if (_findInFilesWindow is null || !_findInFilesWindow.IsLoaded)
        {
            _findInFilesWindow = new FindInFilesWindow(this, folder, seed) { Owner = this };
            _findInFilesWindow.Closed += (_, _) => _findInFilesWindow = null;
        }

        _findInFilesWindow.Show();
        _findInFilesWindow.Activate();
    }

    /// <summary>Runs a folder-wide search off the UI thread and shows the results panel.</summary>
    public async void FindInFiles(string search, string folder, string filter, bool matchCase, bool recursive)
    {
        if (string.IsNullOrEmpty(search) || !Directory.Exists(folder))
            return;

        string[] patterns = filter.Split(new[] { ';', ',' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (patterns.Length == 0)
            patterns = new[] { "*" };

        FindResultsHeader.Text = $"Searching \"{search}\"…";
        FindResultsList.ItemsSource = null;
        FindResultsPanel.Visibility = Visibility.Visible;

        try
        {
            List<FindResult> results = await Task.Run(
                () => SearchFolder(folder, search, patterns, matchCase, recursive));

            FindResultsList.ItemsSource = results;
            int files = results.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            FindResultsHeader.Text = results.Count == 0
                ? $"No matches for \"{search}\""
                : $"{results.Count:N0} match(es) in {files:N0} file(s) for \"{search}\""
                  + (results.Count >= MaxFindResults ? "  —  showing first " + MaxFindResults.ToString("N0") : "");
        }
        catch (Exception ex)
        {
            // async void: an escaping exception would take the whole app down, so the
            // search must always end gracefully even if the folder misbehaves.
            FindResultsHeader.Text = $"Search failed: {ex.Message}";
        }
    }

    private static List<FindResult> SearchFolder(
        string folder, string search, string[] patterns, bool matchCase, bool recursive)
    {
        var results = new List<FindResult>();
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        // Materialise the file list up front so a lazy-enumeration error on a protected
        // subfolder is caught here rather than thrown mid-loop.
        var files = new List<string>();
        foreach (string pattern in patterns)
        {
            try { files.AddRange(Directory.EnumerateFiles(folder, pattern, options)); }
            catch { /* skip a pattern that cannot be expanded */ }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            if (results.Count >= MaxFindResults)
                return results;
            if (!seen.Add(file))
                continue;

            try
            {
                if (new FileInfo(file).Length > 4 * 1024 * 1024)
                    continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    int col = lines[i].IndexOf(search, comparison);
                    if (col < 0)
                        continue;

                    results.Add(new FindResult
                    {
                        FilePath = file,
                        Line = i + 1,
                        Column = col + 1,
                        Length = search.Length,
                        Location = $"{Path.GetFileName(file)}:{i + 1}",
                        Preview = lines[i].Trim(),
                    });

                    if (results.Count >= MaxFindResults)
                        return results;
                }
            }
            catch
            {
                // Unreadable/locked/binary file: skip it and keep searching.
            }
        }

        return results;
    }

    private void FindResults_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindResultsList.SelectedItem is FindResult result)
            OpenSearchResult(result);
    }

    private void FindResults_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && FindResultsList.SelectedItem is FindResult result)
        {
            e.Handled = true;
            OpenSearchResult(result);
        }
    }

    private void OpenSearchResult(FindResult result)
    {
        OpenFile(result.FilePath);
        if (ActiveEditor is not { } ed)
            return;

        int index = CharIndexOf(ed.Text, result.Line, result.Column);
        ed.Select(index, Math.Min(result.Length, Math.Max(0, ed.Text.Length - index)));
        ScrollSelectionIntoView(index);
        ed.Focus();
    }

    private static int CharIndexOf(string text, int line, int column)
    {
        int current = 1, index = 0;
        while (current < line && index < text.Length)
            if (text[index++] == '\n')
                current++;
        return Math.Min(index + (column - 1), text.Length);
    }

    private void CloseFindResults_Click(object sender, RoutedEventArgs e) =>
        FindResultsPanel.Visibility = Visibility.Collapsed;

    // ------------------------------------------------------------------- Go To

    private void ShowGoTo()
    {
        if (ActiveEditor is not { } ed)
            return;

        int currentLine = GetLogicalLine(ed.SelectionStart, ed.Text);
        var dialog = new GoToWindow(currentLine, GetLogicalLineCount(ed.Text)) { Owner = this };
        if (dialog.ShowDialog() == true)
            GoToLine(dialog.LineNumber);
    }

    private void GoToLine(int lineNumber)
    {
        if (ActiveEditor is not { } ed)
            return;

        string text = ed.Text;
        int line = 1, start = 0;
        for (int i = 0; i < text.Length && line < lineNumber; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                start = i + 1;
            }
        }

        ed.CaretIndex = start;
        ed.Select(start, 0);
        ScrollSelectionIntoView(start);
        ed.Focus();
    }

    // ----------------------------------------------------------- Format / View

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        _wordWrap = WordWrapMenuItem.IsChecked;
        foreach (var tab in _docs)
            ApplyWordWrap(tab);
    }

    private void ApplyWordWrap(DocumentTab tab)
    {
        tab.Editor.TextWrapping = _wordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        tab.Editor.HorizontalScrollBarVisibility =
            _wordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void AutoIndent_Click(object sender, RoutedEventArgs e) =>
        _autoIndent = AutoIndentMenuItem.IsChecked;

    private void LineNumbers_Click(object sender, RoutedEventArgs e)
    {
        _showLineNumbers = LineNumbersMenuItem.IsChecked;
        foreach (var tab in _docs)
            tab.View.ShowLineNumbers = _showLineNumbers;
    }

    private void StatusBar_Click(object sender, RoutedEventArgs e) =>
        StatusBarControl.Visibility = StatusBarMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

    private void ChooseFont()
    {
        var dialog = new FontWindow(_fontFamily, _baseFontSizePoints, _bold, _italic) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        _fontFamily = dialog.SelectedFamily;
        _bold = dialog.Bold;
        _italic = dialog.Italic;
        _baseFontSizePoints = dialog.SelectedSizePoints;

        foreach (var tab in _docs)
            ApplyFormattingTo(tab);
        ApplyZoom(_zoom);
    }

    private void LineEnding_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor is not { } ed || sender is not MenuItem { Tag: string kind })
            return;

        string normalized = ed.Text.Replace("\r\n", "\n").Replace('\r', '\n');
        ed.Text = kind switch
        {
            "LF" => normalized,
            "CR" => normalized.Replace('\n', '\r'),
            _ => normalized.Replace("\n", "\r\n"),
        };
        SyncFormatMenus();
    }

    private void Encoding_Click(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab || sender is not MenuItem { Tag: string kind })
            return;

        tab.Encoding = kind switch
        {
            "utf-8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf-16le" => Encoding.Unicode,
            "utf-16be" => Encoding.BigEndianUnicode,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        tab.IsDirty = true;
        UpdateEncodingInfo();
        SyncFormatMenus();
    }

    private void StepZoom(double delta) => ApplyZoom(Math.Round(_zoom + delta, 2));

    private void ApplyZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.2, 5.0);
        foreach (var tab in _docs)
        {
            tab.Editor.FontSize = _baseFontSizePoints * PointsToDip * _zoom;
            tab.View.RefreshGutter();
        }
        ZoomInfo.Text = $"{Math.Round(_zoom * 100)}%";
    }

    private void ApplyFormattingTo(DocumentTab tab)
    {
        var ed = tab.Editor;
        ed.FontFamily = _fontFamily;
        ed.FontWeight = _bold ? FontWeights.Bold : FontWeights.Normal;
        ed.FontStyle = _italic ? FontStyles.Italic : FontStyles.Normal;
        ed.FontSize = _baseFontSizePoints * PointsToDip * _zoom;
        tab.View.ShowLineNumbers = _showLineNumbers;
        ApplyWordWrap(tab);
        tab.View.RefreshGutter();
    }

    private void Editor_PreviewKeyDown(DocumentTab tab, KeyEventArgs e)
    {
        if (!_autoIndent || e.Key != Key.Return)
            return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) != 0)
            return;

        var ed = tab.Editor;
        string text = ed.Text;
        int start = ed.SelectionStart;
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;

        var indent = new StringBuilder();
        for (int i = lineStart; i < start && (text[i] == ' ' || text[i] == '\t'); i++)
            indent.Append(text[i]);

        ed.SelectedText = "\r\n" + indent;
        ed.CaretIndex = start + 2 + indent.Length;
        ed.SelectionLength = 0;
        e.Handled = true;
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            StepZoom(e.Delta > 0 ? +0.1 : -0.1);
            e.Handled = true;
        }
    }

    // ------------------------------------------------------ Drag & drop support

    private void Editor_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Editor_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            foreach (string file in files)
                OpenFile(file);
    }

    // -------------------------------------------------------- Editor / status

    private void OnEditorTextChanged(DocumentTab tab)
    {
        if (_isLoadingFile)
            return;

        tab.IsDirty = true;
        tab.View.SetTotalLines(GetLogicalLineCount(tab.Editor.Text));

        if (!ReferenceEquals(tab, Active))
            return;

        UpdateDocumentInfo();
        UpdateLineEndingInfo();
        UpdatePositionInfo();
        UpdateTitle();
    }

    private void RefreshStatus()
    {
        UpdateDocumentInfo();
        UpdatePositionInfo();
        UpdateLineEndingInfo();
        UpdateEncodingInfo();
    }

    private void UpdateTitle()
    {
        var tab = Active;
        string name = tab?.Name ?? "Untitled";
        Title = $"{(tab?.IsDirty == true ? "*" : "")}{name} — {AppName}";
    }

    private void UpdateDocumentInfo()
    {
        if (ActiveEditor is not { } ed)
            return;
        string text = ed.Text;
        DocInfo.Text = $"{text.Length:N0} chars  ·  {GetLogicalLineCount(text):N0} lines";
    }

    private void UpdatePositionInfo()
    {
        if (ActiveEditor is not { } ed)
            return;

        string text = ed.Text;
        int caret = ed.SelectionStart;
        int line = 1, lineStart = 0;
        int limit = Math.Min(caret, text.Length);
        for (int i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        int column = caret - lineStart + 1;
        int selection = ed.SelectionLength;
        PositionInfo.Text = selection > 0
            ? $"Ln {line}, Col {column}   ({selection:N0} selected)"
            : $"Ln {line}, Col {column}";
    }

    private void UpdateEncodingInfo() =>
        EncodingInfo.Text = DescribeEncoding(Active?.Encoding ?? new UTF8Encoding(false));

    private void UpdateLineEndingInfo()
    {
        if (ActiveEditor is not { } ed)
            return;
        LineEndingInfo.Text = DescribeLineEnding(ed.Text);
    }

    private void SyncFormatMenus()
    {
        if (ActiveEditor is not { } ed || Active is not { } tab)
            return;

        string ending = DetectLineEnding(ed.Text);
        CrlfMenuItem.IsChecked = ending == "CRLF";
        LfMenuItem.IsChecked = ending == "LF";
        CrMenuItem.IsChecked = ending == "CR";

        string enc = EncodingKey(tab.Encoding);
        Utf8MenuItem.IsChecked = enc == "utf-8";
        Utf8BomMenuItem.IsChecked = enc == "utf-8-bom";
        Utf16LeMenuItem.IsChecked = enc == "utf-16le";
        Utf16BeMenuItem.IsChecked = enc == "utf-16be";
    }

    private static string DetectLineEnding(string text)
    {
        if (text.Contains("\r\n"))
            return "CRLF";
        if (text.Contains('\n'))
            return "LF";
        return text.Contains('\r') ? "CR" : "CRLF";
    }

    private static string DescribeLineEnding(string text) => DetectLineEnding(text) switch
    {
        "LF" => "Unix (LF)",
        "CR" => "Macintosh (CR)",
        _ => "Windows (CRLF)",
    };

    private static string EncodingKey(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "utf-8-bom" : "utf-8",
        _ when encoding.Equals(Encoding.Unicode) => "utf-16le",
        _ when encoding.Equals(Encoding.BigEndianUnicode) => "utf-16be",
        _ => "utf-8",
    };

    private static string DescribeEncoding(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8",
        _ when encoding.Equals(Encoding.Unicode) => "UTF-16 LE",
        _ when encoding.Equals(Encoding.BigEndianUnicode) => "UTF-16 BE",
        _ => encoding.WebName.ToUpperInvariant(),
    };

    private static int GetLogicalLine(int charIndex, string text)
    {
        int line = 1;
        int limit = Math.Min(charIndex, text.Length);
        for (int i = 0; i < limit; i++)
            if (text[i] == '\n')
                line++;
        return line;
    }

    private static int GetLogicalLineCount(string text)
    {
        int lines = 1;
        foreach (char c in text)
            if (c == '\n')
                lines++;
        return lines;
    }

    // ------------------------------------------------------- Settings / session

    private void ApplySettingsToGlobals()
    {
        try { _fontFamily = new FontFamily(_settings.FontFamily); }
        catch { _fontFamily = new FontFamily("Consolas"); }

        _baseFontSizePoints = _settings.FontSize;
        _bold = _settings.Bold;
        _italic = _settings.Italic;
        _zoom = Math.Clamp(_settings.Zoom, 0.2, 5.0);
        _wordWrap = _settings.WordWrap;
        _showLineNumbers = _settings.ShowLineNumbers;
        _autoIndent = _settings.AutoIndent;

        WordWrapMenuItem.IsChecked = _wordWrap;
        AutoIndentMenuItem.IsChecked = _autoIndent;
        LineNumbersMenuItem.IsChecked = _showLineNumbers;
        StatusBarMenuItem.IsChecked = _settings.StatusBarVisible;
        StatusBarControl.Visibility = _settings.StatusBarVisible ? Visibility.Visible : Visibility.Collapsed;
        ZoomInfo.Text = $"{Math.Round(_zoom * 100)}%";

        _recentFiles.Clear();
        _recentFiles.AddRange(_settings.RecentFiles);
        RebuildRecentMenu();
    }

    private void RestoreWindowBounds()
    {
        Width = Math.Max(MinWidth, _settings.WindowWidth);
        Height = Math.Max(MinHeight, _settings.WindowHeight);

        if (_settings.WindowLeft is double left && _settings.WindowTop is double top && IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (_settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private static bool IsOnScreen(double left, double top)
    {
        double minX = SystemParameters.VirtualScreenLeft;
        double minY = SystemParameters.VirtualScreenTop;
        double maxX = minX + SystemParameters.VirtualScreenWidth;
        double maxY = minY + SystemParameters.VirtualScreenHeight;
        return left >= minX - 50 && top >= minY - 50 && left <= maxX - 100 && top <= maxY - 100;
    }

    private void RestoreSession()
    {
        var commandLineFiles = Environment.GetCommandLineArgs()
            .Skip(1)
            .Where(a => !a.StartsWith('-') && !a.StartsWith('/'))
            .ToList();

        var toOpen = commandLineFiles.Count > 0 ? commandLineFiles : _settings.OpenFiles;
        foreach (string path in toOpen)
        {
            if (!File.Exists(path))
                continue;
            try
            {
                var (text, encoding) = ReadFile(path);
                var tab = CreateEmptyTab(select: false);
                LoadInto(tab, text, Path.GetFullPath(path), encoding);
            }
            catch
            {
                // Skip files that vanished or became unreadable since last session.
            }
        }

        if (_docs.Count == 0)
            CreateEmptyTab(select: false);

        int index = commandLineFiles.Count > 0
            ? _docs.Count - 1
            : Math.Clamp(_settings.ActiveTab, 0, _docs.Count - 1);
        Tabs.SelectedIndex = index;
    }

    private void SaveSettings()
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }
        else if (!RestoreBounds.IsEmpty)
        {
            _settings.WindowLeft = RestoreBounds.Left;
            _settings.WindowTop = RestoreBounds.Top;
            _settings.WindowWidth = RestoreBounds.Width;
            _settings.WindowHeight = RestoreBounds.Height;
        }
        _settings.WindowMaximized = WindowState == WindowState.Maximized;

        _settings.FontFamily = _fontFamily.Source;
        _settings.FontSize = _baseFontSizePoints;
        _settings.Bold = _bold;
        _settings.Italic = _italic;
        _settings.Zoom = _zoom;
        _settings.WordWrap = _wordWrap;
        _settings.ShowLineNumbers = _showLineNumbers;
        _settings.AutoIndent = _autoIndent;
        _settings.StatusBarVisible = StatusBarMenuItem.IsChecked;

        _settings.RecentFiles = new List<string>(_recentFiles);
        _settings.OpenFiles = _docs.Where(d => d.FilePath is not null).Select(d => d.FilePath!).ToList();
        _settings.ActiveTab = Math.Max(0, Tabs.SelectedIndex);

        _settings.Save();
    }

    // ------------------------------------------------------------------ Window

    /// <summary>Invoked during OS session end: returns false when the user cancels
    /// (or a save fails), signalling that the shutdown should be blocked.</summary>
    public bool PromptToSaveBeforeShutdown()
    {
        foreach (var tab in _docs.ToList())
            if (!ConfirmClose(tab))
                return false;
        return true;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        foreach (var tab in _docs.ToList())
        {
            if (!ConfirmClose(tab))
            {
                e.Cancel = true;
                return;
            }
        }

        SaveSettings();
        _findWindow?.Close();
        _findInFilesWindow?.Close();
    }
}
