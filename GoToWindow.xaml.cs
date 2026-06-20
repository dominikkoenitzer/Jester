using System.Windows;

namespace Jester;

public partial class GoToWindow : ThemedWindow
{
    private readonly int _maxLine;

    public int LineNumber { get; private set; }

    public GoToWindow(int currentLine, int maxLine)
    {
        InitializeComponent();
        _maxLine = Math.Max(1, maxLine);
        PromptText.Text = $"Line number (1 – {_maxLine}):";
        LineBox.Text = currentLine.ToString();
        Loaded += (_, _) =>
        {
            LineBox.Focus();
            LineBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LineBox.Text.Trim(), out int line) || line < 1)
        {
            MessageBox.Show(this, "Please enter a valid line number.",
                "Go To Line", MessageBoxButton.OK, MessageBoxImage.Warning);
            LineBox.SelectAll();
            LineBox.Focus();
            return;
        }

        LineNumber = Math.Min(line, _maxLine);
        DialogResult = true;
    }
}
