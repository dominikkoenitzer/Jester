using System.IO;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Xunit;

namespace Jester.Tests;

/// <summary>Which files get which language, and that every definition loads with colours
/// the palettes know.</summary>
public class SyntaxLanguagesTests
{
    [Theory]
    [InlineData(@"C:\src\main.rs", "Rust")]
    [InlineData(@"C:\src\LIB.RS", "Rust")]
    [InlineData(@"C:\src\App.tsx", "TypeScript")]
    [InlineData(@"C:\src\index.MJS", "TypeScript")]
    [InlineData(@"C:\src\site.css", "CSS")]
    [InlineData(@"C:\src\theme.SCSS", "CSS")]
    [InlineData(@"C:\src\index.html", "HTML")]
    public void AFileGetsItsLanguageFromItsNameOrExtension(string path, string language) =>
        Assert.Equal(language, SyntaxLanguages.ForPath(path)?.Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\notes\todo.txt")]
    [InlineData(@"C:\notes\LICENSE")]
    [InlineData(@"C:\notes\archive.rs.bak")]
    public void OtherFilesStayPlainText(string? path) => Assert.Null(SyntaxLanguages.ForPath(path));

    [Fact]
    public void NoExtensionOrFileNameBelongsToTwoLanguages()
    {
        var extensions = SyntaxLanguages.All.SelectMany(l => l.Extensions).ToList();
        var names = SyntaxLanguages.All.SelectMany(l => l.FileNames).ToList();
        Assert.Equal(extensions.Count, extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(extensions, e => Assert.StartsWith(".", e));
    }

    [Fact]
    public void EveryLanguageLoadsWithEveryColourFromThePalette()
    {
        foreach (var language in SyntaxLanguages.All)
        {
            var definition = SyntaxLanguages.Definition(language.Name);
            Assert.Equal(language.Name, definition.Name);
            Assert.NotEmpty(definition.NamedHighlightingColors);
            Assert.All(definition.NamedHighlightingColors, c => Assert.IsType<SyntaxBrush>(c.Foreground));
        }
    }

    [Fact]
    public void EveryDefinitionFileIsListedAsALanguage()
    {
        var resources = typeof(SyntaxLanguages).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Jester.Syntax.", StringComparison.Ordinal))
            .Select(n => Path.GetFileNameWithoutExtension(n["Jester.Syntax.".Length..]))
            .Order();
        Assert.Equal(SyntaxLanguages.All.Select(l => l.Name).Order(), resources);
    }

    [Fact]
    public void ADefinitionIsLoadedOnceAndShared() =>
        Assert.Same(SyntaxLanguages.Definition("Rust"), SyntaxLanguages.DefinitionFor(@"C:\x\lib.rs"));

    [Theory]
    [InlineData("Keyword", SyntaxKind.Keyword)]
    [InlineData("Keyword.Control", SyntaxKind.Keyword)]
    [InlineData("Heading", SyntaxKind.Heading)]
    public void AColourNameStandsForItsKind(string name, SyntaxKind kind) =>
        Assert.Equal(kind, SyntaxLanguages.KindOf(name));

    [Theory]
    [InlineData("Keywords")]
    [InlineData("keyword")]
    [InlineData("")]
    public void AColourNameThatIsNoKindIsAnError(string name) =>
        Assert.Throws<FormatException>(() => SyntaxLanguages.KindOf(name));

    // Body text needs 4.5:1 against its background to read comfortably.
    [Theory]
    [InlineData(SyntaxScheme.Pastel, true)]
    [InlineData(SyntaxScheme.Pastel, false)]
    [InlineData(SyntaxScheme.Neutral, true)]
    [InlineData(SyntaxScheme.Neutral, false)]
    public void EveryColourReadsOnTheEditorBackground(SyntaxScheme scheme, bool dark)
    {
        var background = EditorBackground(dark);
        foreach (var kind in Enum.GetValues<SyntaxKind>())
        {
            double contrast = Contrast(SyntaxPalette.ColorOf(scheme, dark, kind), background);
            Assert.True(contrast >= 4.5, $"{scheme} {(dark ? "dark" : "light")} {kind}: {contrast:F2}:1");
        }
    }

    private static Color EditorBackground(bool dark)
    {
        string file = Path.Combine(RepositoryRoot(), "Themes", dark ? "Dark.xaml" : "Light.xaml");
        var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(file),
            "<Color x:Key=\"EditorBackgroundColor\">(#[0-9A-Fa-f]{6})</Color>");
        Assert.True(match.Success, "EditorBackgroundColor not found in " + file);
        return (Color)ColorConverter.ConvertFromString(match.Groups[1].Value);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Jester.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Jester.sln not found above the test output.");
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Linear(byte v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }
}
