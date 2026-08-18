using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Jester;

/// <summary>Creating, switching and closing document tabs.</summary>
public partial class MainWindow
{
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
}
