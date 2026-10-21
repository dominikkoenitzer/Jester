using Xunit;

namespace Jester.Tests;

public class JSONHighlightingTests
{
    private const string Sample = """
        {
          // settings
          "name": "jester",
          "path": "C:\\Tools\\a \"b\" c",
          "count": -12.5e3,
          "tags": ["one", "two"],
          "ok": true, "none": null,
          /* JSON5 */ unquoted: 'single', "list": [],
        }
        """;

    [Theory]
    [InlineData("// settings", SyntaxKind.Comment)]
    [InlineData("\"name\"", SyntaxKind.Property)]
    [InlineData("\"jester\"", SyntaxKind.String)]
    [InlineData("\"path\"", SyntaxKind.Property)]
    [InlineData("\\\"", SyntaxKind.Escape)]
    [InlineData("-12.5e3", SyntaxKind.Number)]
    [InlineData("\"one\"", SyntaxKind.String)]
    [InlineData("true", SyntaxKind.Constant)]
    [InlineData("\"none\"", SyntaxKind.Property)]
    [InlineData("null", SyntaxKind.Constant)]
    [InlineData("/* JSON5 */", SyntaxKind.Comment)]
    [InlineData("unquoted", SyntaxKind.Property)]
    [InlineData("'single'", SyntaxKind.String)]
    [InlineData("\"list\"", SyntaxKind.Property)]
    public void TokensGetTheirKind(string token, SyntaxKind kind) =>
        Assert.Equal(kind, Highlight.KindOf("JSON", Sample, token));

    [Fact]
    public void AnEscapedQuoteDoesNotEndTheString() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("JSON", Sample, "\"b"));

    [Fact]
    public void AValueWithAColonInItIsNotAKey() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("JSON", "{\"url\": \"https://example.org\"}", "\"https://example.org\""));
}
