using System.Text;

namespace Jester;

/// <summary>Keeping the title bar and status bar in step with the document.</summary>
public partial class MainWindow
{
    // -------------------------------------------------------- Editor / status

    private void OnEditorTextChanged(DocumentTab tab)
    {
        if (_isLoadingFile)
            return;

        tab.IsDirty = true;
        ScheduleSessionSave();

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
        Title = $"{(tab?.IsDirty == true ? "*" : "")}{name} - {AppName}";
    }

    private void UpdateDocumentInfo()
    {
        if (Active?.View.Lines is not { } lines)
            return;
        DocInfo.Text = $"{lines.Length:N0} chars  ·  {lines.Count:N0} lines";
    }

    private void UpdatePositionInfo()
    {
        if (Active is not { } tab)
            return;

        var ed = tab.Editor;
        var (line, column) = tab.View.Lines.PositionOf(ed.SelectionStart);
        int selection = ed.SelectionLength;
        PositionInfo.Text = selection > 0
            ? $"Ln {line}, Col {column}   ({selection:N0} selected)"
            : $"Ln {line}, Col {column}";
    }

    /// <summary>The 1-based line and column of a caret position, as the status bar shows them.</summary>
    internal static (int Line, int Column) LineAndColumnAt(string text, int caret)
    {
        int line = 1, lineStart = 0;
        int limit = Math.Min(caret, text.Length);
        for (int i = 0; i < limit; i++)
        {
            if (IsLineBreak(text, i))
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, caret - lineStart + 1);
    }

    private void UpdateEncodingInfo() =>
        EncodingInfo.Text = DescribeEncoding(Active?.Encoding ?? new UTF8Encoding(false));

    private void UpdateLineEndingInfo()
    {
        if (Active is not { } tab)
            return;
        LineEndingInfo.Text = DescribeLineEnding(tab.View.Lines.LineEnding);
    }

    private void SyncFormatMenus()
    {
        if (Active is not { } tab)
            return;

        string ending = tab.View.Lines.LineEnding;
        CrlfMenuItem.IsChecked = ending == "CRLF";
        LfMenuItem.IsChecked = ending == "LF";
        CrMenuItem.IsChecked = ending == "CR";

        string enc = EncodingKey(tab.Encoding);
        Utf8MenuItem.IsChecked = enc == "utf-8";
        Utf8BomMenuItem.IsChecked = enc == "utf-8-bom";
        Utf16LeMenuItem.IsChecked = enc == "utf-16le";
        Utf16BeMenuItem.IsChecked = enc == "utf-16be";
    }

    internal static string DetectLineEnding(string text)
    {
        if (text.Contains("\r\n"))
            return "CRLF";
        if (text.Contains('\n'))
            return "LF";
        return text.Contains('\r') ? "CR" : "CRLF";
    }

    private static string DescribeLineEnding(string ending) => ending switch
    {
        "LF" => "Unix (LF)",
        "CR" => "Macintosh (CR)",
        _ => "Windows (CRLF)",
    };

    internal static string EncodingKey(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "utf-8-bom" : "utf-8",
        _ when encoding.Equals(Encoding.Unicode) => "utf-16le",
        _ when encoding.Equals(Encoding.BigEndianUnicode) => "utf-16be",
        // Any other encoding (an ANSI code page) matches no menu item, rather than claiming UTF-8.
        _ => encoding.WebName,
    };

    private static string DescribeEncoding(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8",
        _ when encoding.Equals(Encoding.Unicode) => "UTF-16 LE",
        _ when encoding.Equals(Encoding.BigEndianUnicode) => "UTF-16 BE",
        _ => encoding.WebName.ToUpperInvariant(),
    };

    // A line break is a newline, or a carriage return that is not part of a CRLF
    // pair. Counting only newlines reported a Macintosh (CR) file as one line while
    // the status bar described it as CR in the same breath.
    private static bool IsLineBreak(string text, int index) =>
        text[index] == '\n' || (text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n'));

    private static int GetLogicalLine(int charIndex, string text)
    {
        int line = 1;
        int limit = Math.Min(charIndex, text.Length);
        for (int i = 0; i < limit; i++)
            if (IsLineBreak(text, i))
                line++;
        return line;
    }

    internal static int GetLogicalLineCount(string text)
    {
        int lines = 1;
        for (int i = 0; i < text.Length; i++)
            if (IsLineBreak(text, i))
                lines++;
        return lines;
    }
}
