using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Jester;

/// <summary>
/// Find and replace, shown over the top right of the editor rather than in a window of
/// its own, so the text stays in view. Enter finds the next match, Shift+Enter the
/// previous one, and Esc goes back to the text.
/// </summary>
public partial class FindPanel : UserControl
{
    public FindPanel()
    {
        InitializeComponent();
        ShowReplace(false);
        UpdateButtonState();
    }

    /// <summary>The window whose editor is searched.</summary>
    public MainWindow? Owner { get; set; }

    public string SearchText
    {
        get => FindBox.Text;
        set => FindBox.Text = value;
    }

    public bool MatchCase
    {
        get => MatchCaseBox.IsChecked == true;
        set => MatchCaseBox.IsChecked = value;
    }

    public bool WrapAround
    {
        get => WrapBox.IsChecked == true;
        set => WrapBox.IsChecked = value;
    }

    public bool IsReplaceShown => ReplaceRow.Visibility == Visibility.Visible;

    public void FocusSearchBox()
    {
        FindBox.Focus();
        FindBox.SelectAll();
    }

    public void ShowReplace(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ReplaceRow.Visibility = visibility;
        ReplaceButton.Visibility = visibility;
        ReplaceAllButton.Visibility = visibility;
    }

    /// <summary>A short note under the boxes, such as "No results".</summary>
    public void ShowStatus(string text) => StatusText.Text = text;

    private void Find(bool searchDown)
    {
        if (SearchText.Length == 0 || Owner is null)
            return;

        ShowStatus(Owner.FindNext(SearchText, MatchCase, WrapAround, searchDown) ? "" : "No results");
    }

    private void Next_Click(object sender, RoutedEventArgs e) => Find(searchDown: true);

    private void Previous_Click(object sender, RoutedEventArgs e) => Find(searchDown: false);

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (SearchText.Length == 0 || Owner is null)
            return;

        ShowStatus(Owner.ReplaceNext(SearchText, ReplaceBox.Text, MatchCase, WrapAround, searchDown: true) ? "" : "No results");
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (SearchText.Length == 0 || Owner is null)
            return;

        int replaced = Owner.ReplaceAll(SearchText, ReplaceBox.Text, MatchCase);
        ShowStatus(replaced switch
        {
            0 => "No results",
            1 => "Replaced 1 match",
            _ => $"Replaced {replaced:N0} matches",
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Owner?.CloseFindPanel();

    private void Option_Click(object sender, RoutedEventArgs e) => ShowStatus("");

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        Find(searchDown: (Keyboard.Modifiers & ModifierKeys.Shift) == 0);
    }

    private void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        Replace_Click(sender, e);
    }

    // Esc closes; Alt and a letter work the options and buttons while the focus is here.
    private void Panel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Owner?.CloseFindPanel();
            return;
        }

        if (e.Key != Key.System || Keyboard.Modifiers != ModifierKeys.Alt)
            return;

        switch (e.SystemKey)
        {
            case Key.C:
                MatchCase = !MatchCase;
                ShowStatus("");
                break;
            case Key.W:
                WrapAround = !WrapAround;
                ShowStatus("");
                break;
            case Key.R when IsReplaceShown:
                Replace_Click(sender, e);
                break;
            case Key.A when IsReplaceShown:
                ReplaceAll_Click(sender, e);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FindHint.Visibility = FindBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowStatus("");
        UpdateButtonState();
    }

    private void ReplaceBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ReplaceHint.Visibility = ReplaceBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateButtonState()
    {
        bool hasText = FindBox.Text.Length > 0;
        PreviousButton.IsEnabled = hasText;
        NextButton.IsEnabled = hasText;
        ReplaceButton.IsEnabled = hasText;
        ReplaceAllButton.IsEnabled = hasText;
    }
}
