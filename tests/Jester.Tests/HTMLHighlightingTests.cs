using Xunit;

namespace Jester.Tests;

public class HTMLHighlightingTests
{
    private const string Sample = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <!-- page head
               over two lines -->
          <style>
            body { margin: 0; }
          </style>
          <script type="module">
            const ready = true;
          </script>
          <script type="application/ld+json">{"name": "const"}</script>
        </head>
        <body class='page' hidden>
          <p>Fish &amp; chips &#8364;</p>
          <img src="a.png" alt="x"/>
        </body>
        </html>
        """;

    [Theory]
    [InlineData("<!DOCTYPE html>", SyntaxKind.Decorator)]
    [InlineData("<html", SyntaxKind.Tag)]
    [InlineData("lang", SyntaxKind.Attribute)]
    [InlineData("\"en\"", SyntaxKind.String)]
    [InlineData("over two lines", SyntaxKind.Comment)]
    [InlineData("<style", SyntaxKind.Tag)]
    [InlineData("body", SyntaxKind.Tag)]
    [InlineData("margin", SyntaxKind.Property)]
    [InlineData("</style>", SyntaxKind.Tag)]
    [InlineData("type", SyntaxKind.Attribute)]
    [InlineData("const", SyntaxKind.Keyword)]
    [InlineData("true", SyntaxKind.Constant)]
    [InlineData("</script>", SyntaxKind.Tag)]
    [InlineData("'page'", SyntaxKind.String)]
    [InlineData("hidden", SyntaxKind.Attribute)]
    [InlineData("&amp;", SyntaxKind.Escape)]
    [InlineData("&#8364;", SyntaxKind.Escape)]
    [InlineData("/>", SyntaxKind.Tag)]
    public void TokensGetTheirKind(string token, SyntaxKind kind) =>
        Assert.Equal(kind, Highlight.KindOf("HTML", Sample, token));

    [Theory]
    [InlineData("Fish")]
    [InlineData("chips")]
    public void TextStaysPlain(string token) => Assert.Null(Highlight.KindOf("HTML", Sample, token));

    [Fact]
    public void AScriptThatIsNotCodeStaysPlain() =>
        Assert.Null(Highlight.KindOf("HTML", Sample, "const", occurrence: 1));

    [Fact]
    public void TheDocumentGoesOnAfterAStyleAndAScript()
    {
        const string code = "<style>a { color: red; }</style><b>x</b><script>let y = 1;</script><i>z</i>";
        Assert.Equal(SyntaxKind.Tag, Highlight.KindOf("HTML", code, "<b"));
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("HTML", code, "let"));
        Assert.Equal(SyntaxKind.Tag, Highlight.KindOf("HTML", code, "<i"));
    }

    [Fact]
    public void ATagRunsOverLines()
    {
        const string code = "<a\n  href=\"/x\"\n  title=\"t\">link</a>";
        Assert.Equal(SyntaxKind.Attribute, Highlight.KindOf("HTML", code, "title"));
        Assert.Null(Highlight.KindOf("HTML", code, "link"));
    }
}
