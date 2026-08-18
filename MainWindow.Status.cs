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
        tab.View.SetTotalLines(GetLogicalLineCount(tab.Editor.Text));

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
        Title = $"{(tab?.IsDirty == true ? "*" : "")}{name} — {AppName}";
    }

    private void UpdateDocumentInfo()
    {
        if (ActiveEditor is not { } ed)
            return;
        string text = ed.Text;
        DocInfo.Text = $"{text.Length:N0} chars  ·  {GetLogicalLineCount(text):N0} lines";
    }

    private void UpdatePositionInfo()
    {
        if (ActiveEditor is not { } ed)
            return;

        string text = ed.Text;
        int caret = ed.SelectionStart;
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
        int selection = ed.SelectionLength;
        PositionInfo.Text = selection > 0
            ? $"Ln {line}, Col {column}   ({selection:N0} selected)"
            : $"Ln {line}, Col {column}";
    }

    private void UpdateEncodingInfo() =>
        EncodingInfo.Text = DescribeEncoding(Active?.Encoding ?? new UTF8Encoding(false));

    private void UpdateLineEndingInfo()
    {
        if (ActiveEditor is not { } ed)
            return;
        LineEndingInfo.Text = DescribeLineEnding(ed.Text);
    }

    private void SyncFormatMenus()
    {
        if (ActiveEditor is not { } ed || Active is not { } tab)
            return;

        string ending = DetectLineEnding(ed.Text);
        CrlfMenuItem.IsChecked = ending == "CRLF";
        LfMenuItem.IsChecked = ending == "LF";
        CrMenuItem.IsChecked = ending == "CR";

        string enc = EncodingKey(tab.Encoding);
        Utf8MenuItem.IsChecked = enc == "utf-8";
        Utf8BomMenuItem.IsChecked = enc == "utf-8-bom";
        Utf16LeMenuItem.IsChecked = enc == "utf-16le";
        Utf16BeMenuItem.IsChecked = enc == "utf-16be";
    }

    private static string DetectLineEnding(string text)
    {
        if (text.Contains("\r\n"))
            return "CRLF";
        if (text.Contains('\n'))
            return "LF";
        return text.Contains('\r') ? "CR" : "CRLF";
    }

    private static string DescribeLineEnding(string text) => DetectLineEnding(text) switch
    {
        "LF" => "Unix (LF)",
        "CR" => "Macintosh (CR)",
        _ => "Windows (CRLF)",
    };

    private static string EncodingKey(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "utf-8-bom" : "utf-8",
        _ when encoding.Equals(Encoding.Unicode) => "utf-16le",
        _ when encoding.Equals(Encoding.BigEndianUnicode) => "utf-16be",
        _ => "utf-8",
    };

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
}
