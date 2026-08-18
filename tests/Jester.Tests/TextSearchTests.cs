using Xunit;

namespace Jester.Tests;

/// <summary>
/// Find and replace. The cases that matter are the boundaries — where a scan
/// starts, whether it wraps, and whether a replacement can match itself.
/// </summary>
public class TextSearchTests
{
    private const string Haystack = "the cat sat on the mat";

    // ---------------------------------------------------------------- forwards

    [Fact]
    public void FindsTheFirstMatchFromTheStart()
    {
        Assert.Equal(0, TextSearch.FindNext(Haystack, "the", 0, true, true, false));
    }

    [Fact]
    public void ResumesAfterTheGivenPosition()
    {
        // "the" appears at 0 and 15. Starting past the first must reach the second.
        Assert.Equal(15, TextSearch.FindNext(Haystack, "the", 3, true, true, false));
    }

    [Fact]
    public void ReturnsMinusOneWhenNothingFollowsAndWrapIsOff()
    {
        Assert.Equal(-1, TextSearch.FindNext(Haystack, "the", 16, true, true, false));
    }

    [Fact]
    public void WrapsToTheTopWhenNothingFollows()
    {
        Assert.Equal(0, TextSearch.FindNext(Haystack, "the", 16, true, true, true));
    }

    [Fact]
    public void RepeatedForwardSearchesCycleThroughEveryMatch()
    {
        // What pressing F3 repeatedly actually does.
        const string term = "the";
        var hits = new List<int>();
        int at = 0;
        for (int i = 0; i < 4; i++)
        {
            int found = TextSearch.FindNext(Haystack, term, at, true, true, true);
            hits.Add(found);
            at = found + term.Length;
        }

        Assert.Equal(new[] { 0, 15, 0, 15 }, hits);
    }

    // --------------------------------------------------------------- backwards

    [Fact]
    public void FindsThePrecedingMatch()
    {
        Assert.Equal(0, TextSearch.FindNext(Haystack, "the", 14, false, true, false));
    }

    [Fact]
    public void ACaretAtTheVeryStartHasNothingBehindIt()
    {
        Assert.Equal(-1, TextSearch.FindNext(Haystack, "the", -1, false, true, false));
    }

    [Fact]
    public void WrapsToTheBottomSearchingBackwards()
    {
        Assert.Equal(15, TextSearch.FindNext(Haystack, "the", -1, false, true, true));
    }

    [Fact]
    public void BackwardsFromPastTheEndIsClampedRatherThanThrowing()
    {
        // The caller passes SelectionStart - 1, and a stale selection can point
        // beyond a shortened document.
        Assert.Equal(15, TextSearch.FindNext(Haystack, "the", 9999, false, true, false));
    }

    // ------------------------------------------------------------------ casing

    [Theory]
    [InlineData(true, -1)]
    [InlineData(false, 0)]
    public void MatchCaseIsHonoured(bool matchCase, int expected)
    {
        Assert.Equal(expected, TextSearch.FindNext(Haystack, "THE", 0, true, matchCase, false));
    }

    // ------------------------------------------------------------------- edges

    [Theory]
    [InlineData("", "x")]
    [InlineData("some text", "")]
    [InlineData("", "")]
    public void EmptyInputsFindNothing(string text, string search)
    {
        Assert.Equal(-1, TextSearch.FindNext(text, search, 0, true, true, true));
        Assert.Equal(-1, TextSearch.FindNext(text, search, 0, false, true, true));
    }

    [Fact]
    public void ATermLongerThanTheTextFindsNothing()
    {
        Assert.Equal(-1, TextSearch.FindNext("ab", "abcdef", 0, true, true, true));
    }

    [Fact]
    public void AMatchAtTheVeryEndIsFound()
    {
        Assert.Equal(19, TextSearch.FindNext(Haystack, "mat", 0, true, true, false));
    }

    // ----------------------------------------------------------- replace all

    [Fact]
    public void ReplacesEveryOccurrence()
    {
        var (text, count) = TextSearch.ReplaceAll(Haystack, "the", "a", true);
        Assert.Equal("a cat sat on a mat", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ReportsZeroAndLeavesTheTextAloneWhenNothingMatches()
    {
        var (text, count) = TextSearch.ReplaceAll(Haystack, "zebra", "x", true);
        Assert.Equal(Haystack, text);
        Assert.Equal(0, count);
    }

    [Fact]
    public void AReplacementContainingTheSearchTermTerminates()
    {
        // The loop must advance past what was matched, not what was written,
        // or "a" -> "aa" rescans its own output forever.
        var (text, count) = TextSearch.ReplaceAll("aaa", "a", "aa", true);
        Assert.Equal("aaaaaa", text);
        Assert.Equal(3, count);
    }

    [Fact]
    public void OverlappingCandidatesAreConsumedLeftToRight()
    {
        // "aaaa" contains "aa" three times if you allow overlap, twice if you
        // do not. An editor's Replace All does not overlap.
        var (text, count) = TextSearch.ReplaceAll("aaaa", "aa", "b", true);
        Assert.Equal("bb", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void AnEmptyReplacementDeletes()
    {
        var (text, count) = TextSearch.ReplaceAll(Haystack, "the ", "", true);
        Assert.Equal("cat sat on mat", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ReplaceAllHonoursMatchCase()
    {
        var sensitive = TextSearch.ReplaceAll("Cat cat CAT", "cat", "dog", true);
        Assert.Equal("Cat dog CAT", sensitive.Text);
        Assert.Equal(1, sensitive.Count);

        var insensitive = TextSearch.ReplaceAll("Cat cat CAT", "cat", "dog", false);
        Assert.Equal("dog dog dog", insensitive.Text);
        Assert.Equal(3, insensitive.Count);
    }

    [Fact]
    public void ReplaceAllPreservesEverythingElseIncludingNewlines()
    {
        const string source = "line one\r\nline two\r\nline three";
        var (text, count) = TextSearch.ReplaceAll(source, "line", "row", true);
        Assert.Equal("row one\r\nrow two\r\nrow three", text);
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData("", "x", "y")]
    [InlineData("some text", "", "y")]
    public void ReplaceAllWithEmptyInputsIsANoOp(string text, string search, string replace)
    {
        var result = TextSearch.ReplaceAll(text, search, replace, true);
        Assert.Equal(text, result.Text);
        Assert.Equal(0, result.Count);
    }
}
