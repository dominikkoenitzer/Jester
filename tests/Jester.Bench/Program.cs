using System.Diagnostics;
using System.Text;

namespace Jester.Bench;

/// <summary>
/// The work the editor did for the status bar, the gutter and the current line on
/// every keystroke, before and after the line index, on 1, 5 and 20 MB documents.
/// Reading the TextBox's Text is the TextBox's own cost and is left out of both.
/// Run from the repository root: dotnet run -c Release --project tests/Jester.Bench
/// </summary>
internal static class Program
{
    // Visible rows the gutter numbers per repaint.
    private const int VisibleRows = 50;

    private static void Main()
    {
        Console.WriteLine("Per keystroke, typing one character; mean of many runs.");
        Console.WriteLine();
        Console.WriteLine($"{"Size",-6} {"Caret",-7} {"Before",12} {"After",12}");
        foreach (int megabytes in new[] { 1, 5, 20 })
        {
            string text = Document(megabytes * 1024 * 1024);
            foreach (var (label, caret) in new[] { ("middle", text.Length / 2), ("end", text.Length) })
            {
                double before = Measure(() => Before(text, caret));
                double after = MeasureAfter(text, caret);
                Console.WriteLine($"{megabytes + " MB",-6} {label,-7} {Format(before),12} {Format(after),12}");
            }
        }
    }

    private static string Document(int length)
    {
        const string line = "The quick brown fox jumps over the lazy dog, then naps again.\r\n";
        var sb = new StringBuilder(length + line.Length);
        while (sb.Length < length)
            sb.Append(line);
        sb.Length = length;
        return sb.ToString();
    }

    private static string Format(double ms) => ms >= 1 ? $"{ms:F2} ms" : $"{ms * 1000:F1} µs";

    // Runs an action until at least half a second has passed; returns the mean in ms.
    private static double Measure(Action action)
    {
        action();
        var watch = Stopwatch.StartNew();
        int runs = 0;
        while (watch.ElapsedMilliseconds < 500 || runs < 5)
        {
            action();
            runs++;
        }
        return watch.Elapsed.TotalMilliseconds / runs;
    }

    private static int _sink;

    // What one keystroke ran before: the line count for the gutter and again for the
    // status bar, the line ending, line and column from TextChanged and again from
    // SelectionChanged, the current line from both events, and the gutter's scan to
    // the first visible line.
    private static void Before(string text, int caret)
    {
        _sink += OldLineCount(text);
        _sink += OldLineCount(text);
        _sink += OldDetectLineEnding(text).Length;
        _sink += OldLineAndColumnAt(text, caret).Line;
        _sink += OldLineAndColumnAt(text, caret).Line;
        _sink += OldLogicalLineAt(text, caret);
        _sink += OldLogicalLineAt(text, caret);
        _sink += OldCountNewlines(text, Math.Max(0, caret - 2000));
    }

    // Typing a character and deleting it again, so the text stays the same size;
    // each half is one keystroke.
    private static double MeasureAfter(string text, int caret)
    {
        string typed = text.Insert(caret, "x");
        var index = new LineIndex(text);
        int firstVisible = index.StartOf(index.LineAt(Math.Max(0, caret - 2000)));
        return Measure(() =>
        {
            After(index, typed, caret, 0, 1, firstVisible);
            After(index, text, caret, 1, 0, firstVisible);
        }) / 2;
    }

    private static void After(LineIndex index, string text, int caret, int removed, int added, int firstVisible)
    {
        index.Apply(text, caret, removed, added);
        _sink += index.Count;
        _sink += index.LineEnding.Length;
        _sink += index.PositionOf(caret).Line;
        _sink += index.LineAt(caret);
        _sink += index.LineAt(firstVisible);
        for (int row = 0, line = index.LineAt(firstVisible); row < VisibleRows; row++, line++)
            _sink += index.IsLineStart(index.StartOf(line)) ? 1 : 0;
    }

    // ------------------------------------------------ The scans as they were

    private static bool IsLineBreak(string text, int index) =>
        text[index] == '\n' || (text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n'));

    private static int OldLineCount(string text)
    {
        int lines = 1;
        for (int i = 0; i < text.Length; i++)
            if (IsLineBreak(text, i))
                lines++;
        return lines;
    }

    private static string OldDetectLineEnding(string text)
    {
        if (text.Contains("\r\n"))
            return "CRLF";
        if (text.Contains('\n'))
            return "LF";
        return text.Contains('\r') ? "CR" : "CRLF";
    }

    private static (int Line, int Column) OldLineAndColumnAt(string text, int caret)
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

    private static int OldLogicalLineAt(string text, int charIndex)
    {
        int line = 1;
        int limit = Math.Min(charIndex, text.Length);
        for (int i = 0; i < limit; i++)
            if (IsLineBreak(text, i))
                line++;
        return line;
    }

    private static int OldCountNewlines(string text, int upTo)
    {
        int count = 0;
        int limit = Math.Min(upTo, text.Length);
        for (int i = 0; i < limit; i++)
            if (text[i] == '\n')
                count++;
        return count;
    }
}
