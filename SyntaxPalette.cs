using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace Jester;

/// <summary>What a stretch of highlighted text is. Every colour a syntax definition
/// names is one of these, so the palettes cover every language at once.</summary>
public enum SyntaxKind
{
    Comment,
    Keyword,
    String,
    Escape,
    Number,
    Constant,
    Type,
    Function,
    Property,
    Variable,
    Tag,
    Attribute,
    Operator,
    Decorator,
    Heading,
    Emphasis,
    Strong,
    Link,
    Code,
}

/// <summary>The two sets of syntax colours to choose from.</summary>
public enum SyntaxScheme
{
    /// <summary>Soft pastels with a pink for keywords.</summary>
    Pastel,

    /// <summary>Quiet, greyed tones that leave the text calm.</summary>
    Neutral,
}

/// <summary>
/// The syntax colours for the chosen scheme in the app's current light or dark mode.
/// Every colour reads at 4.5:1 or more on the editor background it is drawn on.
/// </summary>
internal static class SyntaxPalette
{
    private static readonly Dictionary<(SyntaxScheme, bool), SolidColorBrush[]> Brushes = new();
    private static SyntaxScheme _scheme = SyntaxScheme.Pastel;

    /// <summary>Raised when the scheme or the light or dark mode changes, so editors can
    /// draw their text again.</summary>
    public static event Action? Changed;

    public static SyntaxScheme Scheme
    {
        get => _scheme;
        set
        {
            if (_scheme == value)
                return;
            _scheme = value;
            NotifyChanged();
        }
    }

    /// <summary>Counts every change, so an editor that was hidden at the time can tell
    /// that it has to draw again when it is shown.</summary>
    public static int Version { get; private set; }

    public static void NotifyChanged()
    {
        Version++;
        Changed?.Invoke();
    }

    /// <summary>The brush for <paramref name="kind"/> in the current scheme and mode.</summary>
    public static SolidColorBrush Brush(SyntaxKind kind) => BrushesFor(_scheme, ThemeManager.IsDark)[(int)kind];

    private static SolidColorBrush[] BrushesFor(SyntaxScheme scheme, bool dark)
    {
        if (!Brushes.TryGetValue((scheme, dark), out var brushes))
        {
            brushes = Enum.GetValues<SyntaxKind>().Select(kind =>
            {
                var brush = new SolidColorBrush(ColorOf(scheme, dark, kind));
                brush.Freeze();
                return brush;
            }).ToArray();
            Brushes[(scheme, dark)] = brushes;
        }
        return brushes;
    }

    internal static Color ColorOf(SyntaxScheme scheme, bool dark, SyntaxKind kind) =>
        (Color)ColorConverter.ConvertFromString(Table(scheme, dark)[(int)kind]);

    // One entry per SyntaxKind, in its order.
    private static string[] Table(SyntaxScheme scheme, bool dark) => (scheme, dark) switch
    {
        (SyntaxScheme.Pastel, true) => PastelDark,
        (SyntaxScheme.Pastel, false) => PastelLight,
        (SyntaxScheme.Neutral, true) => NeutralDark,
        _ => NeutralLight,
    };

    private static readonly string[] PastelDark =
    [
        "#9399B2", // Comment
        "#FFBBDF", // Keyword
        "#A6E3A1", // String
        "#F5C2E7", // Escape
        "#FAB387", // Number
        "#FAB387", // Constant
        "#F9E2AF", // Type
        "#89B4FA", // Function
        "#B4BEFE", // Property
        "#F2CDCD", // Variable
        "#CBA6F7", // Tag
        "#F9E2AF", // Attribute
        "#89DCEB", // Operator
        "#94E2D5", // Decorator
        "#FFBBDF", // Heading
        "#EBA0AC", // Emphasis
        "#EBA0AC", // Strong
        "#89B4FA", // Link
        "#A6E3A1", // Code
    ];

    // The same hues, deep enough to read on the light paper.
    private static readonly string[] PastelLight =
    [
        "#6C6F85", // Comment
        "#B84A8C", // Keyword
        "#218500", // String
        "#B8489D", // Escape
        "#D33A00", // Number
        "#D33A00", // Constant
        "#AD6000", // Type
        "#1E66F5", // Function
        "#5768DA", // Property
        "#B65557", // Variable
        "#8839EF", // Tag
        "#AD6000", // Attribute
        "#0079B7", // Operator
        "#008087", // Decorator
        "#B84A8C", // Heading
        "#D63446", // Emphasis
        "#D63446", // Strong
        "#1E66F5", // Link
        "#218500", // Code
    ];

    private static readonly string[] NeutralDark =
    [
        "#86858F", // Comment
        "#ADBEE2", // Keyword
        "#AAC8A3", // String
        "#E0B29E", // Escape
        "#E0BD97", // Number
        "#E1B1A1", // Constant
        "#D6CB9F", // Type
        "#A2CCDD", // Function
        "#9DC7C7", // Property
        "#DCB1B3", // Variable
        "#C3B6DD", // Tag
        "#D6CB9F", // Attribute
        "#A4A3AC", // Operator
        "#9EC9B9", // Decorator
        "#E4E3F1", // Heading
        "#D7D6E4", // Emphasis
        "#E4E3F1", // Strong
        "#A2CCDD", // Link
        "#AAC8A3", // Code
    ];

    private static readonly string[] NeutralLight =
    [
        "#686773", // Comment
        "#495A80", // Keyword
        "#45653E", // String
        "#7C4D38", // Escape
        "#775225", // Number
        "#7D4C3B", // Constant
        "#675A27", // Type
        "#326274", // Function
        "#326464", // Property
        "#794D50", // Variable
        "#60527B", // Tag
        "#675A27", // Attribute
        "#686871", // Operator
        "#356656", // Decorator
        "#2D2C39", // Heading
        "#2D2C39", // Emphasis
        "#2D2C39", // Strong
        "#326274", // Link
        "#45653E", // Code
    ];
}

/// <summary>A syntax definition's colour that looks itself up in <see cref="SyntaxPalette"/>
/// each time a line is drawn, so a new scheme or mode needs no new definitions.</summary>
internal sealed class SyntaxBrush(SyntaxKind kind) : HighlightingBrush
{
    public SyntaxKind Kind { get; } = kind;

    public override Brush GetBrush(ITextRunConstructionContext context) => SyntaxPalette.Brush(Kind);

    public override string ToString() => Kind.ToString();
}
