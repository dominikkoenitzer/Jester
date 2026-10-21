using Xunit;

namespace Jester.Tests;

public class YAMLHighlightingTests
{
    private const string Sample = """
        %YAML 1.2
        ---
        # the build
        name: Build it's fine
        "on": [push, "pull_request"]
        defaults: &base
          retries: 3
          timeout: .5
          debug: false
          empty: ~
        jobs:
          test:
            <<: *base
            runs-on: windows-latest
            steps:
              - uses: setup@v4
              - name: 'Run the ''tests'''
                run: |
                  dotnet test
                  echo "key: not a key"

              - run: >-  # folded
                  one
                  two
            env: !!map { path: "C:\tools", level: 2 }
        version: 1.2.3
        note: true love
        ...
        """;

    [Theory]
    [InlineData("%YAML 1.2", SyntaxKind.Decorator)]
    [InlineData("---", SyntaxKind.Operator)]
    [InlineData("# the build", SyntaxKind.Comment)]
    [InlineData("name", SyntaxKind.Property)]
    [InlineData("\"on\"", SyntaxKind.Property)]
    [InlineData("\"pull_request\"", SyntaxKind.String)]
    [InlineData("&base", SyntaxKind.Decorator)]
    [InlineData("retries", SyntaxKind.Property)]
    [InlineData("3", SyntaxKind.Number)]
    [InlineData(".5", SyntaxKind.Number)]
    [InlineData("false", SyntaxKind.Constant)]
    [InlineData("~", SyntaxKind.Constant)]
    [InlineData("<<", SyntaxKind.Property)]
    [InlineData("*base", SyntaxKind.Decorator)]
    [InlineData("runs-on", SyntaxKind.Property)]
    [InlineData("-", SyntaxKind.Operator, 5)]
    [InlineData("uses", SyntaxKind.Property)]
    [InlineData("'Run the ''tests'''", SyntaxKind.String)]
    [InlineData("''", SyntaxKind.Escape)]
    [InlineData("|", SyntaxKind.Operator)]
    [InlineData("dotnet test", SyntaxKind.String)]
    [InlineData("echo \"key: not a key\"", SyntaxKind.String)]
    [InlineData(">-", SyntaxKind.Operator)]
    [InlineData("one", SyntaxKind.String)]
    [InlineData("two", SyntaxKind.String)]
    [InlineData("env", SyntaxKind.Property)]
    [InlineData("!!map", SyntaxKind.Decorator)]
    [InlineData("path", SyntaxKind.Property)]
    [InlineData("\\t", SyntaxKind.Escape)]
    [InlineData("level", SyntaxKind.Property)]
    [InlineData("2", SyntaxKind.Number, 1)]
    [InlineData("version", SyntaxKind.Property)]
    [InlineData("...", SyntaxKind.Operator)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("YAML", Sample, token, occurrence));

    [Theory]
    [InlineData("Build it's fine")]
    [InlineData("push")]
    [InlineData("windows-latest")]
    [InlineData("1.2.3")]
    [InlineData("true love")]
    public void PlainValuesStayPlain(string token) => Assert.Null(Highlight.KindOf("YAML", Sample, token));

    [Fact]
    public void ABlockOfTextEndsAtItsKeysIndentation()
    {
        Assert.Equal(SyntaxKind.Property, Highlight.KindOf("YAML", Sample, "run", occurrence: 2));
        Assert.Equal(SyntaxKind.Comment, Highlight.KindOf("YAML", Sample, "# folded"));
    }

    [Fact]
    public void AHashInABlockOfTextIsText()
    {
        const string text = "script: |\n  # setup\n  make # all\nnext: 1\n";
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("YAML", text, "# setup"));
        Assert.Equal(SyntaxKind.String, Highlight.KindOf("YAML", text, "# all"));
        Assert.Equal(SyntaxKind.Property, Highlight.KindOf("YAML", text, "next"));
    }
}
