using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Jester;

public partial class MainWindow : ThemedWindow
{
    private const string AppName = "Jester";
    private const double PointsToDip = 96.0 / 72.0;

    private string? _currentFilePath;
    private bool _isDirty;
    private bool _isLoadingFile;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private double _baseFontSizePoints = 11;
    private double _zoom = 1.0;

    // Last-used search settings, shared between the Find/Replace dialog and F3.
    private string _searchText = "";
    private bool _matchCase;
    private bool _wrapAround = true;

    private FindReplaceWindow? _findWindow;

    public MainWindow()
    {
        InitializeComponent();
        RegisterCommands();
        ApplyZoom(1.0);
        UpdateTitle();
        string initialText = Editor.Text;
        UpdateDocumentInfo(initialText);
        UpdatePositionInfo(initialText);
        Loaded += (_, _) => Editor.Focus();
    }

    private void RegisterCommands()
    {
        void Bind(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler? canExecute = null)
            => CommandBindings.Add(new CommandBinding(command, handler, canExecute));

        Bind(ApplicationCommands.New, (_, e) => { e.Handled = true; NewFile(); });
        Bind(ApplicationCommands.Open, (_, e) => { e.Handled = true; OpenViaDialog(); });
        Bind(ApplicationCommands.Save, (_, e) => { e.Handled = true; Save(); });
        Bind(JesterCommands.SaveAs, (_, e) => { e.Handled = true; SaveAs(); });
        Bind(JesterCommands.Exit, (_, e) => { e.Handled = true; Close(); });

        Bind(ApplicationCommands.Find, (_, e) => { e.Handled = true; ShowFindReplace(replaceMode: false); });
        Bind(ApplicationCommands.Replace, (_, e) => { e.Handled = true; ShowFindReplace(replaceMode: true); });
        Bind(JesterCommands.FindNext, (_, e) => { e.Handled = true; FindNextFromMenu(searchDown: true); });
        Bind(JesterCommands.FindPrevious, (_, e) => { e.Handled = true; FindNextFromMenu(searchDown: false); });
        Bind(JesterCommands.GoTo, (_, e) => { e.Handled = true; ShowGoTo(); });
        Bind(JesterCommands.InsertDateTime, (_, e) => { e.Handled = true; InsertDateTime(); });

        Bind(JesterCommands.ExportPdf, (_, e) => { e.Handled = true; ExportToPdf(); });
        Bind(JesterCommands.ChooseFont, (_, e) => { e.Handled = true; ChooseFont(); });
        Bind(JesterCommands.ZoomIn, (_, e) => { e.Handled = true; StepZoom(+0.1); });
        Bind(JesterCommands.ZoomOut, (_, e) => { e.Handled = true; StepZoom(-0.1); });
        Bind(JesterCommands.RestoreZoom, (_, e) => { e.Handled = true; ApplyZoom(1.0); });

        Bind(JesterCommands.About, (_, e) => { e.Handled = true; ShowAbout(); });
    }

    // ---------------------------------------------------------------- File ops

    private void NewFile()
    {
        if (!ConfirmDiscardChanges())
            return;

        LoadText(string.Empty, path: null, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void OpenViaDialog()
    {
        if (!ConfirmDiscardChanges())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Open",
            Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
            OpenFile(dialog.FileName);
    }

    /// <summary>Loads a file from disk into the editor. Intended for callers that have
    /// already confirmed any pending changes (command line, Open dialog, drag &amp; drop).</summary>
    public void OpenFile(string path)
    {
        try
        {
            var (text, encoding) = ReadFile(path);
            LoadText(text, path, encoding);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not open the file.\n\n{ex.Message}",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool Save() => _currentFilePath is null ? SaveAs() : WriteToFile(_currentFilePath);

    private bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save As",
            Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = _currentFilePath is null ? "Untitled.txt" : Path.GetFileName(_currentFilePath),
            DefaultExt = ".txt",
            AddExtension = true,
        };

        return dialog.ShowDialog(this) == true && WriteToFile(dialog.FileName);
    }

    private void ExportToPdf()
    {
        string suggested = (_currentFilePath is null
            ? "Untitled"
            : Path.GetFileNameWithoutExtension(_currentFilePath)) + ".pdf";

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
            string title = _currentFilePath is null ? "Untitled" : Path.GetFileName(_currentFilePath);
            PdfExporter.Export(dialog.FileName, Editor.Text, title, Editor.FontFamily.Source, _baseFontSizePoints);

            var open = MessageBox.Show(this,
                "PDF exported successfully.\n\nOpen it now?",
                AppName, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not export the PDF.\n\n{ex.Message}",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool WriteToFile(string path)
    {
        try
        {
            SafeWrite(path, Editor.Text, _encoding);
            _currentFilePath = path;
            _isDirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not save the file.\n\n{ex.Message}",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Writes the text to a temporary file in the same directory and then
    /// atomically swaps it into place, so an interrupted or failed write can never
    /// truncate or corrupt the existing file.</summary>
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

    private void LoadText(string text, string? path, Encoding encoding)
    {
        _isLoadingFile = true;
        Editor.Text = text;
        Editor.CaretIndex = 0;
        Editor.ScrollToHome();
        _isLoadingFile = false;

        _currentFilePath = path;
        _encoding = encoding;
        _isDirty = false;

        UpdateTitle();
        UpdateEncodingInfo();
        UpdateLineEndingInfo(text);
        UpdateDocumentInfo(text);
        UpdatePositionInfo(text);
    }

    /// <summary>Reads a text file, honouring a byte-order mark if present and
    /// defaulting to UTF-8 otherwise. Returns the text and the detected encoding so
    /// the document can be saved back in the same format.</summary>
    private static (string Text, Encoding Encoding) ReadFile(string path)
    {
        using var reader = new StreamReader(path,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        return (text, reader.CurrentEncoding);
    }

    /// <summary>Returns true if it is safe to discard the current document.</summary>
    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty)
            return true;

        string name = _currentFilePath is null ? "Untitled" : Path.GetFileName(_currentFilePath);
        var result = MessageBox.Show(this,
            $"Do you want to save changes to {name}?",
            AppName, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    // ------------------------------------------------------------- Edit extras

    private void InsertDateTime()
    {
        string stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
        int caret = Editor.SelectionStart;
        Editor.SelectedText = stamp;
        Editor.CaretIndex = caret + stamp.Length;
        Editor.Focus();
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

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (Editor.SelectionLength > 0 &&
            string.Equals(Editor.SelectedText, search, comparison))
        {
            int start = Editor.SelectionStart;
            Editor.SelectedText = replace;
            Editor.Select(start, replace.Length);
        }

        return DoFind(searchDown);
    }

    public int ReplaceAll(string search, string replace, bool matchCase)
    {
        if (string.IsNullOrEmpty(search))
            return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        string text = Editor.Text;
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
            int caret = Editor.CaretIndex;
            Editor.Text = builder.ToString();
            Editor.CaretIndex = Math.Min(caret, Editor.Text.Length);
        }

        return count;
    }

    private bool DoFind(bool searchDown)
    {
        if (string.IsNullOrEmpty(_searchText))
            return false;

        string text = Editor.Text;
        if (text.Length == 0)
            return false;

        var comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int index;

        if (searchDown)
        {
            int start = Math.Min(Editor.SelectionStart + Editor.SelectionLength, text.Length);
            index = text.IndexOf(_searchText, start, comparison);
            if (index < 0 && _wrapAround)
                index = text.IndexOf(_searchText, 0, comparison);
        }
        else
        {
            int start = Editor.SelectionStart - 1;
            index = start >= 0 ? text.LastIndexOf(_searchText, start, comparison) : -1;
            if (index < 0 && _wrapAround)
                index = text.LastIndexOf(_searchText, text.Length - 1, comparison);
        }

        if (index < 0)
            return false;

        Editor.Select(index, _searchText.Length);
        ScrollSelectionIntoView(index);
        return true;
    }

    private void ScrollSelectionIntoView(int charIndex)
    {
        try
        {
            int line = Editor.GetLineIndexFromCharacterIndex(charIndex);
            if (line >= 0)
                Editor.ScrollToLine(line);
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
        if (_findWindow is null || !_findWindow.IsLoaded)
        {
            _findWindow = new FindReplaceWindow(this) { Owner = this };
            _findWindow.Closed += (_, _) => _findWindow = null;
        }

        // Seed the search box with a single-line selection, like Notepad does,
        // otherwise fall back to the previous search term.
        string selected = Editor.SelectedText;
        if (Editor.SelectionLength > 0 && !selected.Contains('\n') && !selected.Contains('\r'))
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

    private void ShowGoTo()
    {
        int currentLine = GetLogicalLine(Editor.SelectionStart, Editor.Text);
        var dialog = new GoToWindow(currentLine, GetLogicalLineCount(Editor.Text)) { Owner = this };
        if (dialog.ShowDialog() == true)
            GoToLine(dialog.LineNumber);
    }

    private void GoToLine(int lineNumber)
    {
        string text = Editor.Text;
        int line = 1, start = 0;
        for (int i = 0; i < text.Length && line < lineNumber; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                start = i + 1;
            }
        }

        Editor.CaretIndex = start;
        Editor.Select(start, 0);
        ScrollSelectionIntoView(start);
        Editor.Focus();
    }

    // ----------------------------------------------------------- Format / View

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        bool wrap = WordWrapMenuItem.IsChecked;
        Editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Editor.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void StatusBar_Click(object sender, RoutedEventArgs e) =>
        StatusBarControl.Visibility = StatusBarMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

    private void ChooseFont()
    {
        var dialog = new FontWindow(
            Editor.FontFamily, _baseFontSizePoints,
            Editor.FontWeight == FontWeights.Bold,
            Editor.FontStyle == FontStyles.Italic)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
        {
            Editor.FontFamily = dialog.SelectedFamily;
            Editor.FontWeight = dialog.Bold ? FontWeights.Bold : FontWeights.Normal;
            Editor.FontStyle = dialog.Italic ? FontStyles.Italic : FontStyles.Normal;
            _baseFontSizePoints = dialog.SelectedSizePoints;
            ApplyZoom(_zoom);
        }
    }

    private void StepZoom(double delta) => ApplyZoom(Math.Round(_zoom + delta, 2));

    private void ApplyZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.2, 5.0);
        Editor.FontSize = _baseFontSizePoints * PointsToDip * _zoom;
        ZoomInfo.Text = $"{Math.Round(_zoom * 100)}%";
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            StepZoom(e.Delta > 0 ? +0.1 : -0.1);
            e.Handled = true;
        }
    }

    private void ShowAbout() =>
        MessageBox.Show(this,
            "Jester\nA lightweight notepad.\n\nVersion 1.0\nBuilt with C# and WPF on .NET 9.",
            "About Jester", MessageBoxButton.OK, MessageBoxImage.Information);

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
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ConfirmDiscardChanges())
            OpenFile(files[0]);
    }

    // -------------------------------------------------------- Editor / status

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingFile)
            return;

        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitle();
        }

        string text = Editor.Text;
        UpdateDocumentInfo(text);
        UpdateLineEndingInfo(text);
        UpdatePositionInfo(text);
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e) => UpdatePositionInfo(Editor.Text);

    private void UpdateTitle()
    {
        string name = _currentFilePath is null ? "Untitled" : Path.GetFileName(_currentFilePath);
        Title = $"{(_isDirty ? "*" : "")}{name} — {AppName}";
    }

    private void UpdateDocumentInfo(string text)
    {
        int lines = GetLogicalLineCount(text);
        DocInfo.Text = $"{text.Length:N0} chars  ·  {lines:N0} lines";
    }

    private void UpdatePositionInfo(string text)
    {
        int caret = Editor.SelectionStart;
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
        int selection = Editor.SelectionLength;
        PositionInfo.Text = selection > 0
            ? $"Ln {line}, Col {column}   ({selection:N0} selected)"
            : $"Ln {line}, Col {column}";
    }

    private void UpdateEncodingInfo() => EncodingInfo.Text = DescribeEncoding(_encoding);

    private void UpdateLineEndingInfo(string text)
    {
        LineEndingInfo.Text = text.Contains("\r\n")
            ? "Windows (CRLF)"
            : text.Contains('\n') ? "Unix (LF)" : "Windows (CRLF)";
    }

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

    // ------------------------------------------------------------------ Window

    /// <summary>Invoked during OS session end: returns false when the user cancels
    /// (or a save fails), signalling that the shutdown should be blocked.</summary>
    public bool PromptToSaveBeforeShutdown() => ConfirmDiscardChanges();

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
            e.Cancel = true;
        else
            _findWindow?.Close();
    }
}
