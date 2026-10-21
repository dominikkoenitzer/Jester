using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Jester;

/// <summary>
/// The languages Jester colours, picked by a file's extension or name. Each one is a
/// syntax definition under Syntax/, built into the app. Its colours are named after a
/// <see cref="SyntaxKind"/> and drawn from <see cref="SyntaxPalette"/>.
/// </summary>
internal static class SyntaxLanguages
{
    /// <summary>A language: the definition's name (as in its file) and the files it is for.</summary>
    internal sealed record Language(string Name, string[] Extensions, string[] FileNames);

    internal static readonly Language[] All =
    [
        new("Rust", [".rs"], []),
        new("TypeScript", [".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs"], []),
        new("CSS", [".css", ".scss", ".less"], []),
        new("HTML", [".html", ".htm", ".xhtml"], []),
        new("Python", [".py", ".pyw", ".pyi"], []),
        new("JSON", [".json", ".jsonc", ".json5", ".geojson", ".webmanifest", ".code-workspace"], [".babelrc", ".eslintrc", ".prettierrc"]),
        new("TOML", [".toml"], ["Cargo.lock", "Pipfile", "poetry.lock"]),
        new("YAML", [".yml", ".yaml"], []),
        new("PowerShell", [".ps1", ".psm1", ".psd1"], []),
    ];

    private static readonly Dictionary<string, IHighlightingDefinition> Loaded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Resolver References = new();

    /// <summary>The language for a file, or null for plain text.</summary>
    public static Language? ForPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string name = Path.GetFileName(path);
        string extension = Path.GetExtension(path);
        foreach (var language in All)
            if (language.FileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return language;
        if (extension.Length == 0)
            return null;
        foreach (var language in All)
            if (language.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return language;
        return null;
    }

    /// <summary>The highlighting for a file, or null for plain text.</summary>
    public static IHighlightingDefinition? DefinitionFor(string? path) =>
        ForPath(path) is { } language ? Definition(language.Name) : null;

    /// <summary>The named definition, loaded once and then shared by every tab.</summary>
    public static IHighlightingDefinition Definition(string name)
    {
        lock (Loaded)
            return Load(name);
    }

    private static IHighlightingDefinition Load(string name)
    {
        if (Loaded.TryGetValue(name, out var definition))
            return definition;

        using var stream = typeof(SyntaxLanguages).Assembly.GetManifestResourceStream($"Jester.Syntax.{name}.xshd")
            ?? throw new ArgumentException($"No syntax definition named {name}.", nameof(name));
        using var reader = XmlReader.Create(stream);
        var xshd = HighlightingLoader.LoadXshd(reader);
        foreach (var color in xshd.Elements.OfType<XshdColor>())
            color.Foreground = new SyntaxBrush(KindOf(color.Name));

        // A definition that uses another one's rules, as HTML does for CSS, loads that
        // one here through the resolver. None of them refers back to the one using it.
        definition = HighlightingLoader.Load(xshd, References);
        Loaded[name] = definition;
        return definition;
    }

    /// <summary>The kind a colour stands for: its name, or the part of it before a dot,
    /// so "Keyword.Control" is a keyword drawn with its own weight or style.</summary>
    internal static SyntaxKind KindOf(string colorName)
    {
        int dot = colorName.IndexOf('.');
        string kind = dot < 0 ? colorName : colorName[..dot];
        return Enum.TryParse<SyntaxKind>(kind, ignoreCase: false, out var parsed)
            ? parsed
            : throw new FormatException($"A syntax colour named {colorName} is not a SyntaxKind.");
    }

    // Lets HTML colour the CSS and script inside it with those languages' own rules.
    private sealed class Resolver : IHighlightingDefinitionReferenceResolver
    {
        public IHighlightingDefinition? GetDefinition(string name) =>
            All.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)) ? Definition(name) : null;
    }
}
