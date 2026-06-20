using System.Windows;
using System.Windows.Input;

namespace Jester;

public partial class FindReplaceWindow : ThemedWindow
{
    private readonly MainWindow _owner;

    public FindReplaceWindow(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        UpdateButtonState();
    }

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

    private bool SearchDown => DownRadio.IsChecked == true;

    public void FocusSearchBox()
    {
        FindBox.Focus();
        FindBox.SelectAll();
    }

    public void ShowReplace(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ReplaceLabel.Visibility = visibility;
        ReplaceBox.Visibility = visibility;
        ReplaceButton.Visibility = visibility;
        ReplaceAllButton.Visibility = visibility;
        Title = show ? "Replace" : "Find";
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (SearchText.Length == 0)
            return;

        if (!_owner.FindNext(SearchText, MatchCase, WrapAround, SearchDown))
            NotFound();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (SearchText.Length == 0)
            return;

        if (!_owner.ReplaceNext(SearchText, ReplaceBox.Text, MatchCase, WrapAround, SearchDown))
            NotFound();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (SearchText.Length == 0)
            return;

        int replaced = _owner.ReplaceAll(SearchText, ReplaceBox.Text, MatchCase);
        MessageBox.Show(this,
            replaced > 0 ? $"Replaced {replaced} occurrence(s)." : $"Cannot find \"{SearchText}\".",
            "Jester", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void FindBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateButtonState();

    private void UpdateButtonState()
    {
        bool hasText = FindBox.Text.Length > 0;
        ReplaceButton.IsEnabled = hasText;
        ReplaceAllButton.IsEnabled = hasText;
    }

    private void NotFound() =>
        MessageBox.Show(this, $"Cannot find \"{SearchText}\".",
            "Jester", MessageBoxButton.OK, MessageBoxImage.Information);
}
