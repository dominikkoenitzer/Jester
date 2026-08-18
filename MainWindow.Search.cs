using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Jester;

/// <summary>Find, replace, find-in-files and go-to-line.</summary>
public partial class MainWindow
{
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

        var (replaced, count) = TextSearch.ReplaceAll(ed.Text, search, replace, matchCase);

        if (count > 0)
        {
            int caret = ed.CaretIndex;
            ed.Text = replaced;
            ed.CaretIndex = Math.Min(caret, ed.Text.Length);
        }

        return count;
    }

    private bool DoFind(bool searchDown)
    {
        if (string.IsNullOrEmpty(_searchText) || ActiveEditor is not { } ed)
            return false;

        string text = ed.Text;

        // Searching down resumes after the current selection so repeated F3
        // advances; searching up starts just before it, for the same reason.
        int from = searchDown ? ed.SelectionStart + ed.SelectionLength : ed.SelectionStart - 1;
        int index = TextSearch.FindNext(text, _searchText, from, searchDown, _matchCase, _wrapAround);

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
}
