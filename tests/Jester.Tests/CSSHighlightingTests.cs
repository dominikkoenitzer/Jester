using Xunit;

namespace Jester.Tests;

public class CSSHighlightingTests
{
    private const string Sample = """
        @import url(https://example.org/base.css);

        /* Layout */
        :root {
            --gap: 1.5rem;
        }

        body > main.page, #intro::before {
            margin: 0 auto;
            border: 1px solid #f0a;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            content: "\201C quoted";
            color: var(--gap) !important;
        }

        @media screen and (max-width: 600px) {
            a:hover { text-decoration: underline; }
        }
        """;

    [Theory]
    [InlineData("@import", SyntaxKind.Decorator)]
    [InlineData("https://example.org/base.css", SyntaxKind.String)]
    [InlineData("/* Layout */", SyntaxKind.Comment)]
    [InlineData(":root", SyntaxKind.Attribute)]
    [InlineData("--gap", SyntaxKind.Variable)]
    [InlineData("1.5rem", SyntaxKind.Number)]
    [InlineData("body", SyntaxKind.Tag)]
    [InlineData(">", SyntaxKind.Operator)]
    [InlineData("main", SyntaxKind.Tag)]
    [InlineData(".page", SyntaxKind.Attribute)]
    [InlineData("#intro", SyntaxKind.Attribute)]
    [InlineData("::before", SyntaxKind.Attribute)]
    [InlineData("margin", SyntaxKind.Property)]
    [InlineData("1px", SyntaxKind.Number)]
    [InlineData("#f0a", SyntaxKind.Constant)]
    [InlineData("grid-template-columns", SyntaxKind.Property)]
    [InlineData("repeat", SyntaxKind.Function)]
    [InlineData("minmax", SyntaxKind.Function)]
    [InlineData("1fr", SyntaxKind.Number)]
    [InlineData("\\201C ", SyntaxKind.Escape)]
    [InlineData("var", SyntaxKind.Function)]
    [InlineData("!important", SyntaxKind.Keyword)]
    [InlineData("@media", SyntaxKind.Decorator)]
    [InlineData("and", SyntaxKind.Keyword)]
    [InlineData("600px", SyntaxKind.Number)]
    [InlineData(":hover", SyntaxKind.Attribute)]
    [InlineData("text-decoration", SyntaxKind.Property)]
    public void TokensGetTheirKind(string token, SyntaxKind kind) =>
        Assert.Equal(kind, Highlight.KindOf("CSS", Sample, token));

    [Theory]
    [InlineData("auto")]
    [InlineData("solid")]
    [InlineData("underline")]
    public void KeywordValuesStayPlain(string token) => Assert.Null(Highlight.KindOf("CSS", Sample, token));

    [Fact]
    public void ANestedSelectorIsNotAProperty()
    {
        const string code = ".card {\n  &:hover { color: red; }\n  li:not(.x) { margin: 0; }\n}";
        Assert.Equal(SyntaxKind.Attribute, Highlight.KindOf("CSS", code, ":hover"));
        Assert.Equal(SyntaxKind.Tag, Highlight.KindOf("CSS", code, "li"));
        Assert.Equal(SyntaxKind.Property, Highlight.KindOf("CSS", code, "color"));
        Assert.Equal(SyntaxKind.Property, Highlight.KindOf("CSS", code, "margin"));
    }

    [Fact]
    public void AValueRunsOverLines()
    {
        const string code = "a {\n  transition:\n    opacity 0.2s,\n    transform 0.3s;\n  color: red;\n}";
        Assert.Equal(SyntaxKind.Number, Highlight.KindOf("CSS", code, "0.3s"));
        Assert.Null(Highlight.KindOf("CSS", code, "transform"));
        Assert.Equal(SyntaxKind.Property, Highlight.KindOf("CSS", code, "color"));
    }

    [Fact]
    public void ScssVariablesAndLineComments()
    {
        const string code = "$accent: #ffbbdf; // pink\n.a { color: $accent; }";
        Assert.Equal(SyntaxKind.Variable, Highlight.KindOf("CSS", code, "$accent"));
        Assert.Equal(SyntaxKind.Comment, Highlight.KindOf("CSS", code, "// pink"));
        Assert.Equal(SyntaxKind.Variable, Highlight.KindOf("CSS", code, "$accent", occurrence: 1));
    }

    [Fact]
    public void ACommentRunsOverLines() =>
        Assert.Equal(SyntaxKind.Comment, Highlight.KindOf("CSS", "/* one\n a { color: red; }\n */ b {}", "color: red"));
}
