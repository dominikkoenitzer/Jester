using Xunit;

namespace Jester.Tests;

public class RustHighlightingTests
{
    private const string Sample = """
        //! Crate notes
        use std::collections::HashMap;

        #[derive(Debug, Clone)]
        pub struct Point<'a> {
            name: &'a str,
        }

        const MAX_LEN: usize = 0x1F;

        /* outer /* nested */ still a comment */
        fn main() -> Result<(), String> {
            let mut map = HashMap::new();
            let raw = r#"C:\path "quoted""#;
            let c = '\n';
            println!("{} {}\t", raw, 3.5e2f64);
            if self.ok() && true { return Ok(()); }
        }
        """;

    [Theory]
    [InlineData("//! Crate notes", SyntaxKind.Comment)]
    [InlineData("still a comment", SyntaxKind.Comment)]
    [InlineData("use", SyntaxKind.Keyword)]
    [InlineData("pub", SyntaxKind.Keyword)]
    [InlineData("fn", SyntaxKind.Keyword)]
    [InlineData("HashMap", SyntaxKind.Type)]
    [InlineData("#[derive(Debug, Clone)]", SyntaxKind.Decorator)]
    [InlineData("'a", SyntaxKind.Variable)]
    [InlineData("MAX_LEN", SyntaxKind.Constant)]
    [InlineData("0x1F", SyntaxKind.Number)]
    [InlineData("main", SyntaxKind.Function)]
    [InlineData("new", SyntaxKind.Function)]
    [InlineData("r#\"C:\\path \"quoted\"\"#", SyntaxKind.String)]
    [InlineData("'\\n'", SyntaxKind.String)]
    [InlineData("println!", SyntaxKind.Decorator)]
    [InlineData("\\t", SyntaxKind.Escape)]
    [InlineData("3.5e2f64", SyntaxKind.Number)]
    [InlineData("self", SyntaxKind.Variable)]
    [InlineData("true", SyntaxKind.Constant)]
    [InlineData("->", SyntaxKind.Operator)]
    public void TokensGetTheirKind(string token, SyntaxKind kind) =>
        Assert.Equal(kind, Highlight.KindOf("Rust", Sample, token));

    [Theory]
    [InlineData("map")]
    [InlineData("name")]
    public void NamesStayPlain(string token) => Assert.Null(Highlight.KindOf("Rust", Sample, token));

    [Fact]
    public void APrimitiveIsAType() =>
        Assert.Equal(SyntaxKind.Type, Highlight.KindOf("Rust", Sample, "str", occurrence: 1));

    [Fact]
    public void AKeywordInsideAStringIsPartOfTheString() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Rust", "let s = \"fn let\";", "fn let"));

    [Fact]
    public void AnEscapedQuoteDoesNotEndTheString()
    {
        const string code = "let s = \"a\\\"b fn\"; let t = 1;";
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Rust", code, "b fn"));
        Assert.Equal(SyntaxKind.Escape, Highlight.KindOf("Rust", code, "\\\""));
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("Rust", code, "let", occurrence: 1));
    }

    [Fact]
    public void AHexEscapeIsColouredWhole() =>
        Assert.Equal(SyntaxKind.Escape, Highlight.KindOf("Rust", "let s = \"\\x41\\u{1F600}\";", "\\u{1F600}"));

    [Fact]
    public void ABackslashAtTheEndOfALineKeepsTheStringOpen() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Rust", "let s = \"one \\\n    two fn\";", "two fn"));
}
