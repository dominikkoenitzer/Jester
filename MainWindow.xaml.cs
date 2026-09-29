using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        // Now holds the tabs of every session taken over, so those files can go.
        SaveSession();
        _docs.CollectionChanged += (_, _) => ScheduleSessionSave();
        Closed += (_, _) => CloseSession();

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
            ed.Text = ed.Text.Remove(caret, DeleteLengthAt(ed.Text, caret));
            ed.CaretIndex = caret;
        }
    }

    // A CRLF pair and a surrogate pair are each one character to the reader, and
    // removing half of either leaves a stray newline or an unpaired code unit.
    private static int DeleteLengthAt(string text, int index)
    {
        if (index + 1 >= text.Length)
            return 1;

        if (text[index] == '\r' && text[index + 1] == '\n')
            return 2;

        return char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
    }

    // ----------------------------------------------------- Settings / session

    private void ApplySettingsToGlobals()
    {
        try { _fontFamily = new FontFamily(_settings.FontFamily); }
        catch { _fontFamily = new FontFamily("Consolas"); }

        _baseFontSizePoints = Math.Clamp(_settings.FontSize, 1, 512);
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

        // The stored session has every tab, unsaved ones included. Until one was ever
        // written, the saved files listed in settings.json are the session.
        int active = _session.HadSessions ? RestoreStoredTabs() : RestoreSavedFiles();

        var openFiles = _docs.Where(d => d.FilePath is not null).Select(d => d.FilePath!).ToList();
        foreach (string path in StartupFiles(openFiles, commandLineFiles).Skip(openFiles.Count))
            OpenStartupFile(path);

        if (_docs.Count == 0)
            CreateEmptyTab(select: false);

        // A command-line file is what the user just asked for, so it gets the focus. Look
        // it up by path: it may have been in the session already, or failed to open.
        string? requestedPath = null;
        if (commandLineFiles.Count > 0)
        {
            try { requestedPath = Path.GetFullPath(commandLineFiles[^1]); }
            catch { /* an invalid path was skipped above as well */ }
        }
        var requested = requestedPath is null ? null : _docs.FirstOrDefault(d =>
            string.Equals(d.FilePath, requestedPath, StringComparison.OrdinalIgnoreCase));

        Tabs.SelectedIndex = requested is not null ? _docs.IndexOf(requested) : active;
    }

    /// <summary>Opens the saved files of the last session; returns the tab to select.</summary>
    private int RestoreSavedFiles()
    {
        foreach (string path in StartupFiles(_settings.OpenFiles, Array.Empty<string>()))
            OpenStartupFile(path);
        return RestoredActiveTab(_docs.Select(d => d.FilePath).ToList(), _settings.OpenFiles, _settings.ActiveTab);
    }

    /// <summary>Opens a file in a new tab, or skips it if it has gone or cannot be read.</summary>
    private DocumentTab? OpenStartupFile(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var (text, encoding) = ReadFile(path);
            var tab = CreateEmptyTab(select: false);
            LoadInto(tab, text, Path.GetFullPath(path), encoding);
            return tab;
        }
        catch
        {
            // Skip files that vanished or became unreadable since last session.
            return null;
        }
    }

    /// <summary>The tab to select for the saved active tab, an index into the session's
    /// files. Found by path, since a file that has gone missing shifts every tab after it.</summary>
    internal static int RestoredActiveTab(IReadOnlyList<string?> tabPaths, IReadOnlyList<string> sessionFiles, int activeTab)
    {
        if (activeTab >= 0 && activeTab < sessionFiles.Count)
        {
            for (int i = 0; i < tabPaths.Count; i++)
                if (string.Equals(tabPaths[i], sessionFiles[activeTab], StringComparison.OrdinalIgnoreCase))
                    return i;
        }
        return Math.Clamp(activeTab, 0, Math.Max(0, tabPaths.Count - 1));
    }

    /// <summary>The active tab as saved: a position among the tabs that have a file, since
    /// only those are saved. An Untitled tab counts as the next file along it.</summary>
    internal static int SessionActiveTab(IReadOnlyList<string?> tabPaths, int selectedIndex) =>
        tabPaths.Take(Math.Max(0, selectedIndex)).Count(p => p is not null);

    /// <summary>The files to open at startup: the previous session, then any command-line
    /// files that are not already part of it. Opening only the command-line files would
    /// drop the session tabs, and closing would then save that smaller list over it.</summary>
    internal static List<string> StartupFiles(IReadOnlyList<string> session, IReadOnlyList<string> commandLine)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in session.Concat(commandLine))
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch { continue; }
            if (seen.Add(full))
                files.Add(full);
        }
        return files;
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
        _settings.ActiveTab = SessionActiveTab(_docs.Select(d => d.FilePath).ToList(), Tabs.SelectedIndex);

        _settings.Save();
    }

    // ------------------------------------------------------------------ Window

    /// <summary>Invoked during OS session end: returns false when the user cancels
    /// (or a save fails), signalling that the shutdown should be blocked. Once the
    /// session is written every unsaved tab is kept, so there is nothing to ask.</summary>
    public bool PromptToSaveBeforeShutdown() => SaveSession() || ConfirmCloseAll();

    private bool ConfirmCloseAll()
    {
        foreach (var tab in _docs.ToList())
            if (!ConfirmClose(tab))
                return false;
        return true;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Unsaved and Untitled tabs come back next start with their text, as in any modern
        // editor, so closing does not ask about them. Only when the session cannot be
        // written would closing lose them, and then the user decides as before.
        if (!SaveSession() && !ConfirmCloseAll())
        {
            e.Cancel = true;
            return;
        }

        SaveSettings();
        _findWindow?.Close();
        _findInFilesWindow?.Close();
    }
}
