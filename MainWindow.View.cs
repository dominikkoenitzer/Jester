using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Jester;

/// <summary>Word wrap, line numbers, font, encoding, line endings and zoom.</summary>
public partial class MainWindow
{
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
}
