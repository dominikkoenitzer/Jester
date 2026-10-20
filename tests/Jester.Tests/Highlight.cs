using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Jester.Tests;

/// <summary>Runs a language's definition over some text, as the editor does line by
/// line, and tells what a piece of it is coloured as.</summary>
internal static class Highlight
{
    /// <summary>The kind the editor draws <paramref name="token"/> in, the innermost
    /// coloured stretch that covers all of it; null when it stays plain text.
    /// <paramref name="occurrence"/> picks a later one of several equal tokens.</summary>
    public static SyntaxKind? KindOf(string language, string text, string token, int occurrence = 0)
    {
        int at = -1;
        for (int i = 0; i <= occurrence; i++)
        {
            at = text.IndexOf(token, at + 1, StringComparison.Ordinal);
            if (at < 0)
                throw new ArgumentException($"\"{token}\" occurs fewer than {occurrence + 1} times.", nameof(token));
        }

        SyntaxKind? kind = null;
        int width = int.MaxValue;
        foreach (var (offset, length, sectionKind) in Sections(language, text))
        {
            if (offset <= at && at + token.Length <= offset + length && length <= width)
            {
                kind = sectionKind;
                width = length;
            }
        }
        return kind;
    }

    /// <summary>Every coloured stretch of the text, outer ones before the ones inside them.</summary>
    public static List<(int Offset, int Length, SyntaxKind Kind)> Sections(string language, string text)
    {
        var document = new TextDocument(text);
        var highlighter = new DocumentHighlighter(document, SyntaxLanguages.Definition(language));
        var sections = new List<(int, int, SyntaxKind)>();
        for (int line = 1; line <= document.LineCount; line++)
        {
            foreach (var section in highlighter.HighlightLine(line).Sections)
            {
                if (section.Color.Foreground is SyntaxBrush brush)
                    sections.Add((section.Offset, section.Length, brush.Kind));
            }
        }
        return sections;
    }
}
