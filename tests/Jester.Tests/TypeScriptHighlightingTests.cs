using Xunit;

namespace Jester.Tests;

public class TypeScriptHighlightingTests
{
    private const string Sample = """
        import { readFile } from "node:fs/promises";

        /** Notes on the config. */
        export const MAX_SIZE = 0x400;

        @Component({ selector: "app" })
        export class Loader<T> implements Source {
            private cache = new Map<string, T>();

            async load(path: string): Promise<T | undefined> {
                const text = await readFile(path, 'utf8');
                const pattern = /^\s*"[a-z]+"$/gi;
                if (this.cache.size > 1.5e3 && text.match(pattern)) {
                    return undefined;
                }
                return JSON.parse(`{"at": ${Date.now()}, "path": "${path}"}`) as T;
            }
        }

        const half = total / 2 / 4;
        const view = <div className="box">{name}</div>;
        """;

    [Theory]
    [InlineData("/** Notes on the config. */", SyntaxKind.Comment)]
    [InlineData("import", SyntaxKind.Keyword)]
    [InlineData("from", SyntaxKind.Keyword)]
    [InlineData("\"node:fs/promises\"", SyntaxKind.String)]
    [InlineData("MAX_SIZE", SyntaxKind.Constant)]
    [InlineData("0x400", SyntaxKind.Number)]
    [InlineData("@Component", SyntaxKind.Decorator)]
    [InlineData("Loader", SyntaxKind.Type)]
    [InlineData("implements", SyntaxKind.Keyword)]
    [InlineData("Map", SyntaxKind.Type)]
    [InlineData("string", SyntaxKind.Type)]
    [InlineData("load", SyntaxKind.Function)]
    [InlineData("readFile", SyntaxKind.Function, 1)]
    [InlineData("'utf8'", SyntaxKind.String)]
    [InlineData("/^\\s*\"[a-z]+\"$/gi", SyntaxKind.String)]
    [InlineData("this", SyntaxKind.Variable)]
    [InlineData("1.5e3", SyntaxKind.Number)]
    [InlineData("&&", SyntaxKind.Operator)]
    [InlineData("undefined", SyntaxKind.Constant, 1)]
    [InlineData("${", SyntaxKind.Escape)]
    [InlineData("now", SyntaxKind.Function)]
    [InlineData("<div", SyntaxKind.Tag)]
    [InlineData("</div>", SyntaxKind.Tag)]
    [InlineData("\"box\"", SyntaxKind.String)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("TypeScript", Sample, token, occurrence));

    [Theory]
    [InlineData("cache")]
    [InlineData("path")]
    [InlineData("text")]
    [InlineData("total")]
    public void NamesStayPlain(string token) => Assert.Null(Highlight.KindOf("TypeScript", Sample, token));

    [Fact]
    public void AMemberNamedLikeAKeywordStaysPlain() =>
        Assert.Null(Highlight.KindOf("TypeScript", "if (node.type === \"x\") {}", "type"));

    [Fact]
    public void ADivisionIsNotAPattern()
    {
        const string code = "const half = total / 2 / 4; let k;";
        Assert.Equal(SyntaxKind.Operator, Highlight.KindOf("TypeScript", code, "/"));
        Assert.Equal(SyntaxKind.Number, Highlight.KindOf("TypeScript", code, "2"));
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("TypeScript", code, "let"));
    }

    [Fact]
    public void AQuoteInsideAPatternDoesNotStartAString() =>
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("TypeScript", "const re = /\"/; const x = 1;", "const", occurrence: 1));

    [Fact]
    public void AnEscapedQuoteDoesNotEndTheString() =>
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("TypeScript", "const s = 'it\\'s fine if'; let y;", "s fine if"));

    [Fact]
    public void ATemplateRunsOverLinesAndAnObjectInItDoesNotEndIt()
    {
        const string code = "const t = `one\n${ {a: 1}.a } two`;\nlet z;";
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("TypeScript", code, "two"));
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("TypeScript", code, "let"));
    }

    [Fact]
    public void ABlockCommentRunsOverLines() =>
        Assert.Equal(SyntaxKind.Comment, Highlight.KindOf("TypeScript", "/* one\n const two\n */ let x;", "const two"));

    [Fact]
    public void ASelfClosingElementIsNotAPattern() =>
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("TypeScript", "const a = <img src={x} />; const b = 1;", "const", occurrence: 1));
}
