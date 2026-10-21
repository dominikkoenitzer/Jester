using Xunit;

namespace Jester.Tests;

public class TOMLHighlightingTests
{
    private const string Sample = """"
        # build settings
        title = "Jester \"notes\""
        [package]
        name = 'jester'
        version-major = 0x1F
        site.home = "https://example.org"  # the page
        released = 2026-10-21T08:00:00Z
        ratio = -1.5e3
        on = true
        tags = [
          "one",
          2,
        ]
        point = { x = 1, "y" = false }
        notes = """
        first = line
        """

        [[bin]]
        path = "src/main.rs"
        """";

    [Theory]
    [InlineData("# build settings", SyntaxKind.Comment)]
    [InlineData("title", SyntaxKind.Property)]
    [InlineData("\\\"", SyntaxKind.Escape)]
    [InlineData("[package]", SyntaxKind.Heading)]
    [InlineData("'jester'", SyntaxKind.String)]
    [InlineData("version-major", SyntaxKind.Property)]
    [InlineData("0x1F", SyntaxKind.Number)]
    [InlineData("home", SyntaxKind.Property)]
    [InlineData("# the page", SyntaxKind.Comment)]
    [InlineData("2026-10-21T08:00:00Z", SyntaxKind.Number)]
    [InlineData("-1.5e3", SyntaxKind.Number)]
    [InlineData("true", SyntaxKind.Constant)]
    [InlineData("\"one\"", SyntaxKind.String)]
    [InlineData("2", SyntaxKind.Number, 3)]
    [InlineData("x", SyntaxKind.Property, 2)]
    [InlineData("\"y\"", SyntaxKind.Property)]
    [InlineData("false", SyntaxKind.Constant)]
    [InlineData("first = line", SyntaxKind.String)]
    [InlineData("[[bin]]", SyntaxKind.Heading)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("TOML", Sample, token, occurrence));

    [Fact]
    public void ABracketInAValueIsNotAHeader() =>
        Assert.Null(Highlight.KindOf("TOML", "list = [\n[1, 2],\n]\n", "[", occurrence: 1));
}
