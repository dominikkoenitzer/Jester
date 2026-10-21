using Xunit;

namespace Jester.Tests;

public class PythonHighlightingTests
{
    private const string Sample = """"
        """Module notes, with "quotes" inside."""
        import os.path
        from dataclasses import dataclass

        MAX_ITEMS = 0x10  # the limit

        @dataclass(frozen=True)
        class Item:
            name: str
            price: float = 1.5e2

            def label(self, width: int = 10) -> str:
                path = os.path.join(r"C:\new", "a\tb")
                return f"{self.name!r:>{width}} costs {{ {self.price * 2} }}"

        async def main():
            match command.split():
                case ["go", where]:
                    await run(where) if where is not None else print(b'\x00')
            total = left @ right
        """";

    [Theory]
    [InlineData("\"\"\"Module notes, with \"quotes\" inside.\"\"\"", SyntaxKind.String)]
    [InlineData("import", SyntaxKind.Keyword)]
    [InlineData("from", SyntaxKind.Keyword)]
    [InlineData("MAX_ITEMS", SyntaxKind.Constant)]
    [InlineData("0x10", SyntaxKind.Number)]
    [InlineData("# the limit", SyntaxKind.Comment)]
    [InlineData("@dataclass", SyntaxKind.Decorator)]
    [InlineData("True", SyntaxKind.Constant)]
    [InlineData("class", SyntaxKind.Keyword, 3)]
    [InlineData("Item", SyntaxKind.Type)]
    [InlineData("str", SyntaxKind.Type)]
    [InlineData("1.5e2", SyntaxKind.Number)]
    [InlineData("def", SyntaxKind.Keyword)]
    [InlineData("label", SyntaxKind.Function)]
    [InlineData("self", SyntaxKind.Variable)]
    [InlineData("->", SyntaxKind.Operator)]
    [InlineData("join", SyntaxKind.Function)]
    [InlineData("r\"C:\\new\"", SyntaxKind.String)]
    [InlineData("\\t", SyntaxKind.Escape)]
    [InlineData("{{", SyntaxKind.Escape)]
    [InlineData("}}", SyntaxKind.Escape, 1)]
    [InlineData(" costs ", SyntaxKind.String)]
    [InlineData("async", SyntaxKind.Keyword)]
    [InlineData("match", SyntaxKind.Keyword)]
    [InlineData("case", SyntaxKind.Keyword)]
    [InlineData("\"go\"", SyntaxKind.String)]
    [InlineData("await", SyntaxKind.Keyword)]
    [InlineData("None", SyntaxKind.Constant)]
    [InlineData("\\x00", SyntaxKind.Escape)]
    [InlineData("@", SyntaxKind.Operator, 1)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("Python", Sample, token, occurrence));

    [Theory]
    [InlineData("name")]
    [InlineData("price")]
    [InlineData("where")]
    [InlineData("total")]
    public void NamesStayPlain(string token) => Assert.Null(Highlight.KindOf("Python", Sample, token));

    [Fact]
    public void AFieldInAnFStringIsCode()
    {
        Assert.Equal(SyntaxKind.Escape, Highlight.KindOf("Python", Sample, "{"));
        Assert.Equal(SyntaxKind.Variable, Highlight.KindOf("Python", Sample, "self", occurrence: 1));
        Assert.Equal(SyntaxKind.Number, Highlight.KindOf("Python", Sample, "2", occurrence: 1));
    }

    [Fact]
    public void ARawStringKeepsItsBackslashes()
    {
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Python", "p = r'a\\nb'", "\\n"));
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Python", "p = r'it\\'s'; q = 1", "s'"));
        Assert.Equal(SyntaxKind.Number, Highlight.KindOf("Python", "p = r'it\\'s'; q = 1", "1"));
    }

    [Fact]
    public void ATripleQuotedStringRunsOverLines() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("Python", "s = '''one\ndef two\n''' \nx = 1", "def two"));

    [Fact]
    public void SoftKeywordsAreNamesElsewhere()
    {
        const string code = "match = re.match(p, s)\ntype = 3\n";
        Assert.Null(Highlight.KindOf("Python", code, "match"));
        Assert.Equal(SyntaxKind.Function, Highlight.KindOf("Python", code, "match", occurrence: 1));
        Assert.NotEqual(SyntaxKind.Keyword, Highlight.KindOf("Python", code, "type"));
    }
}
