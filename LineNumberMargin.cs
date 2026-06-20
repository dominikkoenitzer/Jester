using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Jester;

/// <summary>
/// A slim margin that paints logical line numbers beside a <see cref="TextBox"/>.
/// It repaints whenever the editor's text, size, font, or scroll position changes and
/// numbers only the first display row of each logical line, so word-wrapped lines are
/// never numbered twice.
/// </summary>
internal sealed class LineNumberMargin : FrameworkElement
{
    private const double LeftPadding = 10;
    private const double RightPadding = 9;

    private readonly TextBox _editor;
    private readonly Brush _background;
    private readonly Brush _separator;
    private readonly Brush _numberBrush;
    private readonly Brush _currentBrush;

    /// <summary>Total logical lines in the document; drives the gutter width.</summary>
    public int TotalLines { get; set; } = 1;

    /// <summary>1-based logical line the caret sits on; rendered emphasised.</summary>
    public int CurrentLine { get; set; } = 1;

    public LineNumberMargin(TextBox editor)
    {
        _editor = editor;

        _background = Frozen(Color.FromRgb(0xF1, 0xEA, 0xDD));
        _separator = Frozen(Color.FromRgb(0xDC, 0xCB, 0xA4));
        _numberBrush = Frozen(Color.FromRgb(0xAA, 0x9F, 0xBC));
        _currentBrush = Frozen(Color.FromRgb(0xC9, 0x97, 0x1F));

        _editor.TextChanged += (_, _) => { InvalidateMeasure(); InvalidateVisual(); };
        _editor.SizeChanged += (_, _) => InvalidateVisual();
        _editor.AddHandler(ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, _) => InvalidateVisual()));
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private Typeface CurrentTypeface =>
        new(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, FontStretches.Normal);

    private double Dpi => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    protected override Size MeasureOverride(Size availableSize)
    {
        int digits = Math.Max(2, TotalLines.ToString(CultureInfo.InvariantCulture).Length);
        var sample = new FormattedText(new string('0', digits), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, CurrentTypeface, _editor.FontSize, _numberBrush, Dpi);

        double width = LeftPadding + sample.Width + RightPadding;
        double height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(_background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.DrawRectangle(_separator, null, new Rect(ActualWidth - 1, 0, 1, ActualHeight));

        int first = _editor.GetFirstVisibleLineIndex();
        int last = _editor.GetLastVisibleLineIndex();
        if (first < 0 || last < first)
            return;

        string text = _editor.Text;
        int firstChar = SafeCharFromLine(first);
        if (firstChar < 0)
            return;

        int logical = CountNewlines(text, firstChar) + 1;
        var typeface = CurrentTypeface;
        var boldTypeface = new Typeface(typeface.FontFamily, typeface.Style, FontWeights.Bold, typeface.Stretch);
        double fontSize = _editor.FontSize;
        double dpi = Dpi;

        for (int i = first; i <= last; i++)
        {
            int charIdx = SafeCharFromLine(i);
            if (charIdx < 0)
                continue;

            bool startsLogicalLine = charIdx == 0 || (charIdx <= text.Length && text[charIdx - 1] == '\n');
            if (i > first && startsLogicalLine)
                logical++;
            if (!startsLogicalLine)
                continue;

            Rect r = _editor.GetRectFromCharacterIndex(charIdx);
            if (r.IsEmpty)
                continue;

            bool isCurrent = logical == CurrentLine;
            var ft = new FormattedText(logical.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                isCurrent ? boldTypeface : typeface, fontSize,
                isCurrent ? _currentBrush : _numberBrush, dpi);

            dc.DrawText(ft, new Point(ActualWidth - RightPadding - ft.Width, r.Top));
        }
    }

    private int SafeCharFromLine(int line)
    {
        try { return _editor.GetCharacterIndexFromLineIndex(line); }
        catch { return -1; }
    }

    private static int CountNewlines(string text, int upTo)
    {
        int count = 0;
        int limit = Math.Min(upTo, text.Length);
        for (int i = 0; i < limit; i++)
            if (text[i] == '\n')
                count++;
        return count;
    }
}
