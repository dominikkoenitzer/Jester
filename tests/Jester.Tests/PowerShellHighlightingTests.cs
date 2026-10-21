using Xunit;

namespace Jester.Tests;

public class PowerShellHighlightingTests
{
    private const string Sample = """
        <#
          .SYNOPSIS
          Copies the notes.
        #>
        [CmdletBinding()]
        param([Parameter(Mandatory)] [string]$Path, [int]$Count = 0x10)

        function Copy-Notes {
            # one at a time
            foreach ($file in Get-ChildItem -Path $Path -Recurse) {
                if ($file.Length -gt 1MB -and $null -ne $env:TEMP) {
                    Write-Host "Big: $($file.Name) `t ""quoted"" $Count"
                }
                $file.CopyTo('C:\It''s here', $true) | Out-Null
            }
            Invoke-Thing @splat
            [Math]::Round(2.5)
        }

        $text = @"
        Hello $Path
        "@
        """;

    [Theory]
    [InlineData(".SYNOPSIS", SyntaxKind.Comment)]
    [InlineData("CmdletBinding", SyntaxKind.Decorator)]
    [InlineData("param", SyntaxKind.Keyword)]
    [InlineData("Parameter", SyntaxKind.Decorator)]
    [InlineData("string", SyntaxKind.Type)]
    [InlineData("$Path", SyntaxKind.Variable)]
    [InlineData("0x10", SyntaxKind.Number)]
    [InlineData("function", SyntaxKind.Keyword)]
    [InlineData("Copy-Notes", SyntaxKind.Function)]
    [InlineData("# one at a time", SyntaxKind.Comment)]
    [InlineData("foreach", SyntaxKind.Keyword)]
    [InlineData("in", SyntaxKind.Keyword, 4)]
    [InlineData("Get-ChildItem", SyntaxKind.Function)]
    [InlineData("-Path", SyntaxKind.Attribute)]
    [InlineData("-Recurse", SyntaxKind.Attribute)]
    [InlineData("-gt", SyntaxKind.Operator)]
    [InlineData("1MB", SyntaxKind.Number)]
    [InlineData("-and", SyntaxKind.Operator)]
    [InlineData("$null", SyntaxKind.Constant)]
    [InlineData("$env:TEMP", SyntaxKind.Variable)]
    [InlineData("Write-Host", SyntaxKind.Function)]
    [InlineData("\"Big: ", SyntaxKind.String)]
    [InlineData("$(", SyntaxKind.Escape)]
    [InlineData("`t", SyntaxKind.Escape)]
    [InlineData("\"\"", SyntaxKind.Escape)]
    [InlineData("$Count", SyntaxKind.Variable, 1)]
    [InlineData("CopyTo", SyntaxKind.Function)]
    [InlineData("'C:\\It''s here'", SyntaxKind.String)]
    [InlineData("''", SyntaxKind.Escape)]
    [InlineData("$true", SyntaxKind.Constant)]
    [InlineData("|", SyntaxKind.Operator)]
    [InlineData("@splat", SyntaxKind.Variable)]
    [InlineData("Math", SyntaxKind.Type)]
    [InlineData("::", SyntaxKind.Operator)]
    [InlineData("Round", SyntaxKind.Function)]
    [InlineData("2.5", SyntaxKind.Number)]
    [InlineData("Hello ", SyntaxKind.String)]
    [InlineData("$Path", SyntaxKind.Variable, 2)]
    public void TokensGetTheirKind(string token, SyntaxKind kind, int occurrence = 0) =>
        Assert.Equal(kind, Highlight.KindOf("PowerShell", Sample, token, occurrence));

    [Fact]
    public void MemberNamesStayPlain()
    {
        Assert.Null(Highlight.KindOf("PowerShell", Sample, "Length"));
        Assert.Equal(SyntaxKind.Text, Highlight.KindOf("PowerShell", Sample, "Name"));
    }

    [Fact]
    public void KeywordsIgnoreCase() =>
        Assert.Equal(SyntaxKind.Keyword, Highlight.KindOf("PowerShell", "IF ($a) { Return }", "Return"));
}
