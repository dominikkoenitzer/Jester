namespace Jester;

/// <summary>
/// Where every line of a document starts, kept up to date edit by edit. A line ends
/// at a newline or at a carriage return that is not part of a CRLF pair, the same
/// rule as the rest of the app, so a Macintosh (CR) file counts its lines too.
/// An edit reads only the changed text and one character either side of it, so
/// neither the status bar nor the gutter needs the whole document per keystroke.
/// </summary>
internal sealed class LineIndex
{
    private enum Kind : byte { CrLf, Lf, Cr }

    // Line k starts at Start(k): 0 for the first line, else just after a break of kind
    // _kinds[k]. An edit moves every later line by the same amount, so rather than
    // rewrite them all it records that as _shift, owed by every entry from _shiftFrom
    // on, and only settles the entries between one edit and the next.
    private readonly List<int> _starts = [0];
    private readonly List<Kind> _kinds = [Kind.CrLf];
    private readonly List<int> _scannedStarts = [];
    private readonly List<Kind> _scannedKinds = [];
    private int _shiftFrom, _shift;
    private int _crlf, _lf, _cr;

    public LineIndex(string text = "") => Reset(text);

    /// <summary>Length of the text the index describes.</summary>
    public int Length { get; private set; }

    /// <summary>Number of lines, at least one.</summary>
    public int Count => _starts.Count;

    /// <summary>"CRLF", "LF" or "CR": CRLF if any pair exists, else LF, else CR, else CRLF.</summary>
    public string LineEnding => _crlf > 0 ? "CRLF" : _lf > 0 ? "LF" : _cr > 0 ? "CR" : "CRLF";

    /// <summary>Indexes the whole text from scratch.</summary>
    public void Reset(string text)
    {
        _starts.RemoveRange(1, _starts.Count - 1);
        _kinds.RemoveRange(1, _kinds.Count - 1);
        _crlf = _lf = _cr = 0;
        _shiftFrom = _shift = 0;
        Scan(text, 0, text.Length, 0, text.Length - 1);
        _starts.AddRange(_scannedStarts);
        _kinds.AddRange(_scannedKinds);
        Length = text.Length;
    }

    /// <summary>
    /// Updates the index after <paramref name="removed"/> characters at
    /// <paramref name="offset"/> were replaced by <paramref name="added"/> new ones,
    /// giving <paramref name="text"/>. Falls back to a full rescan when the change
    /// does not fit the text it describes.
    /// </summary>
    public void Apply(string text, int offset, int removed, int added) =>
        Apply(offset, removed, added, text.Length, text.Substring);

    /// <summary>
    /// The same, for a text too costly to hand over whole: <paramref name="read"/>
    /// returns a stretch of it, given where the stretch starts and how long it is.
    /// Only the changed characters and one either side of them are read, unless the
    /// change does not fit, which reads the whole text.
    /// </summary>
    public void Apply(int offset, int removed, int added, int newLength, Func<int, int, string> read)
    {
        if (offset < 0 || removed < 0 || added < 0 || offset + removed > Length ||
            Length - removed + added != newLength)
        {
            Reset(read(0, newLength));
            return;
        }

        // Whether index i is a break depends on text[i] and text[i + 1], and its kind
        // on text[i - 1]. So the breaks that can change lie from one before the edit
        // to one past it; everything outside that window only moves.
        int lo = Math.Max(0, offset - 1);
        int oldHi = Math.Min(offset + removed, Length - 1);
        int newHi = Math.Min(offset + added, newLength - 1);

        int first = LowerBound(lo + 1);
        int last = LowerBound(oldHi + 2);
        SettleShiftBefore(last);
        for (int k = first; k < last; k++)
            Tally(_kinds[k], -1);
        _starts.RemoveRange(first, last - first);
        _kinds.RemoveRange(first, last - first);

        // Every line after the edit moves by the size of the change.
        _shiftFrom = first;
        _shift += added - removed;

        // The window, plus the character before and after it that decide its breaks.
        int from = Math.Max(0, lo - 1);
        int to = Math.Min(newLength, newHi + 2);
        Scan(to > from ? read(from, to - from) : "", from, newLength, lo, newHi);
        _starts.InsertRange(first, _scannedStarts);
        _kinds.InsertRange(first, _scannedKinds);
        _shiftFrom += _scannedStarts.Count;

        Length = newLength;
    }

    /// <summary>The 1-based line holding a character index; past the end is the last line.</summary>
    public int LineAt(int charIndex) => Math.Max(1, LowerBound(charIndex + 1));

    /// <summary>Where a 1-based line starts, clamped to the lines that exist.</summary>
    public int StartOf(int line) => Start(Math.Clamp(line, 1, _starts.Count) - 1);

    /// <summary>Whether a character index is the first character of a line.</summary>
    public bool IsLineStart(int charIndex)
    {
        int line = LineAt(charIndex);
        return Start(line - 1) == charIndex;
    }

    /// <summary>The 1-based line and column of a caret position, as the status bar shows them.</summary>
    public (int Line, int Column) PositionOf(int caret)
    {
        int line = LineAt(caret);
        return (line, caret - Start(line - 1) + 1);
    }

    // Collects every break at an index in [from, to] of a text of the given length, in
    // order, and counts it. The part of the text at hand starts at index partStart.
    private void Scan(string part, int partStart, int length, int from, int to)
    {
        _scannedStarts.Clear();
        _scannedKinds.Clear();
        for (int i = from; i <= to; i++)
        {
            char c = part[i - partStart];
            Kind kind;
            if (c == '\n')
                kind = i > 0 && part[i - 1 - partStart] == '\r' ? Kind.CrLf : Kind.Lf;
            else if (c == '\r' && (i + 1 == length || part[i + 1 - partStart] != '\n'))
                kind = Kind.Cr;
            else
                continue;

            _scannedStarts.Add(i + 1);
            _scannedKinds.Add(kind);
            Tally(kind, 1);
        }
    }

    private void Tally(Kind kind, int by)
    {
        if (kind == Kind.CrLf)
            _crlf += by;
        else if (kind == Kind.Lf)
            _lf += by;
        else
            _cr += by;
    }

    private int Start(int k) => k >= _shiftFrom ? _starts[k] + _shift : _starts[k];

    // Makes the entries before k exact and moves the owed shift to start at k.
    private void SettleShiftBefore(int k)
    {
        if (_shift != 0)
        {
            for (int i = _shiftFrom; i < k; i++)
                _starts[i] += _shift;
            for (int i = k; i < _shiftFrom; i++)
                _starts[i] -= _shift;
        }
        _shiftFrom = k;
    }

    // The first k with Start(k) >= value (Count if none).
    private int LowerBound(int value)
    {
        int lo = 0, hi = _starts.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (Start(mid) < value)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
