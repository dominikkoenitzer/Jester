using ICSharpCode.AvalonEdit.Document;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// The line index the status bar and the gutter read instead of rescanning the text.
/// After any edit to its document it must say exactly what a full scan of the new text
/// says, CR, LF and CRLF alike, including edits that join or split a CRLF pair.
/// </summary>
public class LineIndexTests
{
    [Theory]
    [InlineData("", 1, "CRLF")]
    [InlineData("one", 1, "CRLF")]
    [InlineData("one\r\ntwo\r\nthree", 3, "CRLF")]
    [InlineData("one\ntwo\nthree", 3, "LF")]
    [InlineData("one\rtwo\rthree", 3, "CR")]
    [InlineData("one\rtwo\r", 3, "CR")]
    [InlineData("one\n\r\ntwo", 3, "CRLF")]
    [InlineData("\r\r\n\n", 4, "CRLF")]
    public void CountsLinesAndTheLineEndingLikeAFullScan(string text, int lines, string ending)
    {
        var index = Index(text);
        Assert.Equal(lines, index.Count);
        Assert.Equal(ending, index.LineEnding);
        Assert.Equal(text.Length, index.Length);
    }

    [Theory]
    [InlineData("one\r\ntwo", 7, 2, 3)]
    [InlineData("one\ntwo", 6, 2, 3)]
    [InlineData("one\rtwo", 6, 2, 3)]
    [InlineData("one\rtwo\r", 8, 3, 1)]
    [InlineData("one", 0, 1, 1)]
    [InlineData("one\ntwo", 99, 2, 96)]
    public void GivesTheSameLineAndColumnAsTheStatusBar(string text, int caret, int line, int column)
    {
        Assert.Equal((line, column), Index(text).PositionOf(caret));
        Assert.Equal((line, column), MainWindow.LineAndColumnAt(text, caret));
    }

    [Theory]
    // Typing a newline after a lone CR turns it into one CRLF break, not two.
    [InlineData("a\rb", 2, 0, "\n", 2, "CRLF")]
    // Typing between CR and LF splits one break into two.
    [InlineData("a\r\nb", 2, 0, "x", 3, "LF")]
    // Deleting the CR of a pair leaves an LF break.
    [InlineData("a\r\nb", 1, 1, "", 2, "LF")]
    // Deleting the LF of a pair leaves a CR break.
    [InlineData("a\r\nb", 2, 1, "", 2, "CR")]
    // Deleting a whole pair joins the lines.
    [InlineData("a\r\nb", 1, 2, "", 1, "CRLF")]
    // A CR at the very end is a break until an LF follows it.
    [InlineData("a\r", 2, 0, "\n", 2, "CRLF")]
    [InlineData("a\r", 2, 0, "b", 2, "CR")]
    // Pasting several lines in the middle of one.
    [InlineData("start end", 5, 1, "\none\r\ntwo\rthree\n", 5, "CRLF")]
    // Replacing everything, as opening a file does.
    [InlineData("x\ny\nz", 0, 5, "p\rq", 2, "CR")]
    public void FollowsEditsThatAddOrRemoveLineBreaks(
        string before, int offset, int removed, string inserted, int lines, string ending)
    {
        string after = before.Remove(offset, removed).Insert(offset, inserted);
        var document = new TextDocument(before);
        var index = new LineIndex(document);

        document.Replace(offset, removed, inserted);

        Assert.Equal(lines, index.Count);
        Assert.Equal(ending, index.LineEnding);
        AssertMatchesFullScan(after, index);
    }

    [Fact]
    public void CountsAWholeNewTextFromScratch()
    {
        var document = new TextDocument("one\ntwo");
        var index = new LineIndex(document);

        document.Text = "a\rb\rc";

        Assert.Equal(3, index.Count);
        Assert.Equal("CR", index.LineEnding);
        AssertMatchesFullScan("a\rb\rc", index);
    }

    [Fact]
    public void FollowsSeveralEditsMadeAsOne()
    {
        var document = new TextDocument("a\rb\r\nc");
        var index = new LineIndex(document);

        document.BeginUpdate();
        document.Insert(2, "\n");
        document.Remove(4, 1);
        document.Insert(document.TextLength, "\r");
        document.EndUpdate();

        Assert.Equal("a\r\nb\nc\r", document.Text);
        AssertMatchesFullScan(document.Text, index);
    }

    [Fact]
    public void FollowsUndoAndRedo()
    {
        var document = new TextDocument("one\r\ntwo");
        var index = new LineIndex(document);

        document.Replace(3, 2, "\n");
        document.Insert(0, "\r");
        AssertMatchesFullScan(document.Text, index);

        while (document.UndoStack.CanUndo)
        {
            document.UndoStack.Undo();
            AssertMatchesFullScan(document.Text, index);
        }
        Assert.Equal("CRLF", index.LineEnding);

        while (document.UndoStack.CanRedo)
        {
            document.UndoStack.Redo();
            AssertMatchesFullScan(document.Text, index);
        }
        Assert.Equal("LF", index.LineEnding);
    }

    [Fact]
    public void KnowsWhichCharactersStartALine()
    {
        var index = Index("ab\r\ncd\ref\ngh");

        int[] starts = [0, 4, 7, 10];
        for (int i = 0; i <= 12; i++)
            Assert.Equal(starts.Contains(i), index.StartOf(index.LineAt(i)) == i);
        Assert.Equal(7, index.StartOf(3));
        Assert.Equal(10, index.StartOf(99));
        Assert.Equal(0, index.StartOf(0));
    }

    [Fact]
    public void StaysExactThroughThousandsOfRandomEdits()
    {
        var random = new Random(96);
        const string alphabet = "ab\r\n\r\n";
        string text = "";
        var document = new TextDocument();
        var index = new LineIndex(document);

        for (int step = 0; step < 5000; step++)
        {
            int offset = random.Next(text.Length + 1);
            int removed = random.Next(Math.Min(4, text.Length - offset) + 1);
            var inserted = new char[random.Next(5)];
            for (int i = 0; i < inserted.Length; i++)
                inserted[i] = alphabet[random.Next(alphabet.Length)];

            text = text.Remove(offset, removed).Insert(offset, new string(inserted));
            document.Replace(offset, removed, new string(inserted));

            AssertMatchesFullScan(text, index);
        }
    }

    private static LineIndex Index(string text) => new(new TextDocument(text));

    private static void AssertMatchesFullScan(string text, LineIndex index)
    {
        Assert.Equal(text.Length, index.Length);
        Assert.Equal(MainWindow.GetLogicalLineCount(text), index.Count);
        Assert.Equal(MainWindow.DetectLineEnding(text), index.LineEnding);
        // Every caret on short texts; on longer ones the ends and a spread between.
        int step = Math.Max(1, text.Length / 64);
        for (int caret = 0; caret <= text.Length + 1; caret += caret < text.Length ? step : 1)
            Assert.Equal(MainWindow.LineAndColumnAt(text, caret), index.PositionOf(caret));
        Assert.Equal(MainWindow.LineAndColumnAt(text, text.Length), index.PositionOf(text.Length));
    }
}
