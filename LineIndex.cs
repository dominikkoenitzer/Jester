using ICSharpCode.AvalonEdit.Document;

namespace Jester;

/// <summary>
/// The lines of an editor's document, for the status bar and the gutter. The document
/// keeps its lines in a tree that every edit updates in place, and ends a line at a
/// newline or at a carriage return that is not part of a CRLF pair, the same rule as
/// the rest of the app, so a Macintosh (CR) file counts its lines too. What it does not
/// keep is how many breaks of each kind there are, which the line ending comes from, so
/// this counts them from each change, reading only the changed text and one character
/// either side of it.
/// </summary>
internal sealed class LineIndex
{
    private readonly TextDocument _document;
    private int _crlf, _lf, _cr;

    public LineIndex(TextDocument document)
    {
        _document = document;
        Tally(0, document.TextLength - 1, 1);

        // Whether index i is a break depends on text[i] and text[i + 1], and its kind on
        // text[i - 1]. So the breaks an edit can change lie from one before it to one past
        // it: those are uncounted before the edit and counted again after it.
        document.Changing += (_, e) =>
        {
            if (e.RemovalLength == document.TextLength)
                _crlf = _lf = _cr = 0; // All of it goes, as when a file is opened.
            else
                Tally(e.Offset - 1, e.Offset + e.RemovalLength, -1);
        };
        document.Changed += (_, e) => Tally(e.Offset - 1, e.Offset + e.InsertionLength, 1);
    }

    /// <summary>Length of the text.</summary>
    public int Length => _document.TextLength;

    /// <summary>Number of lines, at least one.</summary>
    public int Count => _document.LineCount;

    /// <summary>"CRLF", "LF" or "CR": CRLF if any pair exists, else LF, else CR, else CRLF.</summary>
    public string LineEnding => _crlf > 0 ? "CRLF" : _lf > 0 ? "LF" : _cr > 0 ? "CR" : "CRLF";

    /// <summary>The 1-based line holding a character index; past the end is the last line.</summary>
    public int LineAt(int charIndex) => _document.GetLineByOffset(Math.Clamp(charIndex, 0, Length)).LineNumber;

    /// <summary>Where a 1-based line starts, clamped to the lines that exist.</summary>
    public int StartOf(int line) => _document.GetLineByNumber(Math.Clamp(line, 1, Count)).Offset;

    /// <summary>The 1-based line and column of a caret position, as the status bar shows them.</summary>
    public (int Line, int Column) PositionOf(int caret)
    {
        int line = LineAt(caret);
        return (line, caret - StartOf(line) + 1);
    }

    // Counts every break at an index in [from, to] of the text as it is now, by the
    // given amount.
    private void Tally(int from, int to, int by)
    {
        int length = _document.TextLength;
        from = Math.Max(0, from);
        to = Math.Min(to, length - 1);
        if (to < from)
            return;

        // The stretch, plus the character before and after it that decide its breaks.
        int partStart = Math.Max(0, from - 1);
        string part = _document.GetText(partStart, Math.Min(length, to + 2) - partStart);
        for (int i = from; i <= to; i++)
        {
            char c = part[i - partStart];
            if (c == '\n')
            {
                if (i > 0 && part[i - 1 - partStart] == '\r')
                    _crlf += by;
                else
                    _lf += by;
            }
            else if (c == '\r' && (i + 1 == length || part[i + 1 - partStart] != '\n'))
            {
                _cr += by;
            }
        }
    }
}
