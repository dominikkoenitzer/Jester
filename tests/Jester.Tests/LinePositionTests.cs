using Xunit;

namespace Jester.Tests;

/// <summary>
/// Go To Line and the status bar's line and column. A line ends at a newline or at
/// a carriage return that is not part of a CRLF pair, the same rule the line count
/// uses, so a Macintosh (CR) file is not one long line.
/// </summary>
public class LinePositionTests
{
    [Theory]
    [InlineData("one\r\ntwo\r\nthree", 3, 10)]
    [InlineData("one\ntwo\nthree", 3, 8)]
    [InlineData("one\rtwo\rthree", 3, 8)]
    [InlineData("one\rtwo\rthree", 2, 4)]
    public void GoToLineFindsTheStartOfTheLine(string text, int line, int expected)
    {
        Assert.Equal(expected, MainWindow.LineStartIndex(text, line));
    }

    [Theory]
    [InlineData("one\ntwo", 1, 0)]
    [InlineData("one\ntwo", 9, 4)]
    [InlineData("one\rtwo", 9, 4)]
    [InlineData("", 3, 0)]
    public void GoToLineStaysInsideTheText(string text, int line, int expected)
    {
        Assert.Equal(expected, MainWindow.LineStartIndex(text, line));
    }

    [Theory]
    [InlineData("one\r\ntwo", 7, 2, 3)]
    [InlineData("one\ntwo", 6, 2, 3)]
    [InlineData("one\rtwo", 6, 2, 3)]
    [InlineData("one\rtwo\r", 8, 3, 1)]
    [InlineData("one", 0, 1, 1)]
    public void TheStatusBarCountsLinesAndColumnsLikeTheLineCount(string text, int caret, int line, int column)
    {
        Assert.Equal((line, column), MainWindow.LineAndColumnAt(text, caret));
    }
}
