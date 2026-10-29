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
        ScheduleSettingsSave();
    }

    // The editor wraps only the lines on screen, and turns its horizontal scroll bar off
    // while it does.
    private void ApplyWordWrap(DocumentTab tab) => tab.Editor.WordWrap = _wordWrap;

    private void AutoIndent_Click(object sender, RoutedEventArgs e)
    {
        _autoIndent = AutoIndentMenuItem.IsChecked;
        ScheduleSettingsSave();
    }

    private void LineNumbers_Click(object sender, RoutedEventArgs e)
    {
        _showLineNumbers = LineNumbersMenuItem.IsChecked;
        foreach (var tab in _docs)
            tab.View.ShowLineNumbers = _showLineNumbers;
        ScheduleSettingsSave();
    }

    private void StatusBar_Click(object sender, RoutedEventArgs e)
    {
        StatusBarControl.Visibility = StatusBarMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        ScheduleSettingsSave();
    }

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
        if (Active is not { } tab || sender is not MenuItem { Tag: string kind })
            return;

        ConvertLineEndings(tab.View, kind);
        // A text with no break does not change, so nothing else updates the status bar.
        UpdateLineEndingInfo();
        SyncFormatMenus();
    }

    /// <summary>Makes every line break <paramref name="kind"/>: "CRLF", "LF" or "CR".</summary>
    internal static void ConvertLineEndings(EditorView view, string kind)
    {
        view.Lines.Chosen = kind;
        var ed = view.Editor;
        string text = ed.Text;
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string converted = kind switch
        {
            "LF" => normalized,
            "CR" => normalized.Replace('\n', '\r'),
            _ => normalized.Replace("\n", "\r\n"),
        };
        if (converted != text)
        {
            // One edit that Undo takes back; setting Text would clear the undo history. Every
            // line keeps its number, so the caret stays on its line and column.
            var document = ed.Document;
            var caret = document.GetLocation(ed.CaretOffset);
            document.Replace(0, document.TextLength, converted);
            ed.Select(document.GetOffset(caret.Line, caret.Column), 0);
        }
    }

    private void Encoding_Click(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab || sender is not MenuItem { Tag: string kind })
            return;

        if (SetEncoding(tab, kind))
        {
            ScheduleSessionSave();
            UpdateEncodingInfo();
            UpdateTitle();
        }
        // A checkable item unchecks itself when clicked, so the menu is put right either way.
        SyncFormatMenus();
    }

    /// <summary>Gives the tab the encoding a menu item names; false when it already has it.</summary>
    internal static bool SetEncoding(DocumentTab tab, string kind)
    {
        Encoding encoding = kind switch
        {
            "utf-8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf-16le" => Encoding.Unicode,
            "utf-16be" => Encoding.BigEndianUnicode,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        if (EncodingKey(encoding) == EncodingKey(tab.Encoding))
            return false;

        tab.Encoding = encoding;
        tab.IsDirty = true;
        return true;
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
        ScheduleSettingsSave();
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
