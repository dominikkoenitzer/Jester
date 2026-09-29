using Xunit;

namespace Jester.Tests;

/// <summary>
/// What Enter inserts. A file keeps the line ending it already has, the one the
/// status bar shows, so pressing Enter in a Unix (LF) file must not leave a
/// Windows (CRLF) line in the middle of it.
/// </summary>
public class LineBreakTests
{
    [Theory]
    [InlineData("one\r\ntwo", "\r\n")]
    [InlineData("one\ntwo", "\n")]
    [InlineData("one\rtwo", "\r")]
    [InlineData("", "\r\n")]
    [InlineData("one line", "\r\n")]
    public void InsertsTheDocumentsOwnLineEnding(string text, string expected)
    {
        Assert.Equal(expected, MainWindow.LineBreakAt(text, text.Length, indent: false));
    }

    [Theory]
    [InlineData("one\r\n  two", "\r\n  ")]
    [InlineData("one\n  two", "\n  ")]
    [InlineData("one\r\ttwo", "\r\t")]
    [InlineData("  one", "\r\n  ")]
    public void KeepsTheIndentOfTheCurrentLine(string text, string expected)
    {
        Assert.Equal(expected, MainWindow.LineBreakAt(text, text.Length, indent: true));
    }

    [Fact]
    public void DoesNotIndentWhenAutoIndentIsOff()
    {
        Assert.Equal("\n", MainWindow.LineBreakAt("one\n  two", 9, indent: false));
    }

    [Fact]
    public void TakesTheIndentOnlyFromBeforeTheCaret()
    {
        // Caret after one of the two leading spaces.
        Assert.Equal("\n ", MainWindow.LineBreakAt("one\n  two", 5, indent: true));
    }
}
