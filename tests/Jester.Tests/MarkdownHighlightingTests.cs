using Xunit;

namespace Jester.Tests;

public class MarkdownHighlightingTests
{
    private const string Sample = """
        # Jester notes

        Some **bold words**, *a slant*, _another_, ~~gone~~ and `inline code`.
        A snake_case_name, 2 * 3 * 4, and a \*literal star\*.
        See [the guide](https://example.org/guide "Guide") or <https://example.org>.
        Plain https://example.org/page. too, and <kbd>Ctrl</kbd>.

        > A quote with *style*.

        - one
        - [x] done
        10. ten

        Title
        =====

        ***

        | Name | Size |
        |:-----|-----:|

        ```python
        def run(): return "go"
        ```

        ```
        # not a heading
        ```

        <!-- a note
        over lines -->
        [guide]: https://example.org/guide
        """;

    [Theory]
    [InlineData("# Jester notes", SyntaxKind.Heading)]
    [InlineData("**bold words**", SyntaxKind.Strong)]
    [InlineData("*a slant*", SyntaxKind.Emphasis)]
    [InlineData("_another_", SyntaxKind.Emphasis)]
    [InlineData("~~gone~~", SyntaxKind.Emphasis)]
    [InlineData("`inline code`", SyntaxKind.Code)]
    [InlineData("\\*", SyntaxKind.Escape)]
    [InlineData("[the guide](https://example.org/guide \"Guide\")", SyntaxKind.Link)]
    [InlineData("<https://example.org>", SyntaxKind.Link)]
    [InlineData("https://example.org/page", SyntaxKind.Link)]
    [InlineData("<kbd>", SyntaxKind.Tag)]
    [InlineData("> A quote with ", SyntaxKind.Comment)]
    [InlineData("*style*", SyntaxKind.Emphasis)]
    [InlineData("-", SyntaxKind.Operator)]
    [InlineData("- [x]", SyntaxKind.Operator)]
    [InlineData("10.", SyntaxKind.Operator)]
    [InlineData("=====", SyntaxKind.Heading)]
    [InlineData("***", SyntaxKind.Operator)]
    [InlineData("|:-----|-----:|", SyntaxKind.Operator)]
    [InlineData("```python", SyntaxKind.Code)]
    [InlineData("def", SyntaxKind.Keyword)]
    [InlineData("run", SyntaxKind.Function)]
    [InlineData("\"go\"", SyntaxKind.String)]
    [InlineData("# not a heading", SyntaxKind.Code)]
    [InlineData("over lines -->", SyntaxKind.Comment)]
    [InlineData("[guide]: https://example.org/guide", SyntaxKind.Link)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("Markdown", Sample, token, occurrence));

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("2 * 3 * 4")]
    [InlineData("literal star")]
    [InlineData("Title")]
    [InlineData("| Name | Size |")]
    public void PlainTextStaysPlain(string token) => Assert.Null(Highlight.KindOf("Markdown", Sample, token));

    [Fact]
    public void AFencedBlockEndsAtItsFence()
    {
        const string text = "```js\nlet a = 1;\n```\nlet b\n";
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("Markdown", text, "let"));
        Assert.Null(Highlight.KindOf("Markdown", text, "let b"));
    }
}
