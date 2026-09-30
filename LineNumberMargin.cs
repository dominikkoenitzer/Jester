using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace Jester;

/// <summary>
/// A slim margin that paints logical line numbers beside a <see cref="TextEditor"/>.
/// It repaints whenever the editor lays out or scrolls its lines, and numbers only the
/// first row of each line, so word-wrapped lines are never numbered twice.
/// </summary>
internal sealed class LineNumberMargin : FrameworkElement
{
    private const double LeftPadding = 10;
    private const double RightPadding = 9;

    private readonly TextEditor _editor;
    private readonly TextView _textView;
    private readonly Brush _background;
    private readonly Brush _separator;
    private readonly Brush _numberBrush;
    private readonly Brush _currentBrush;

    /// <summary>Total logical lines in the document; drives the gutter width.</summary>
    public int TotalLines { get; set; } = 1;

    /// <summary>1-based logical line the caret sits on; rendered emphasised.</summary>
    public int CurrentLine { get; set; } = 1;

    public LineNumberMargin(TextEditor editor)
    {
        _editor = editor;
        _textView = editor.TextArea.TextView;

        _background = Frozen(Color.FromRgb(0xF1, 0xEA, 0xDD));
        _separator = Frozen(Color.FromRgb(0xDC, 0xCB, 0xA4));
        _numberBrush = Frozen(Color.FromRgb(0xAA, 0x9F, 0xBC));
        _currentBrush = Frozen(Color.FromRgb(0xC9, 0x97, 0x1F));

        // The view rebuilds its lines after an edit, a resize or a font change. It is laid
        // out after the gutter, so where its lines sit is only known once it has a size.
        _textView.VisualLinesChanged += (_, _) => InvalidateVisual();
        _textView.ScrollOffsetChanged += (_, _) => InvalidateVisual();
        _textView.SizeChanged += (_, _) => InvalidateVisual();
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

        // Before its template is applied the view is not yet beside the gutter.
        if (!_textView.VisualLinesValid || FindCommonVisualAncestor(_textView) is null)
            return;

        var typeface = CurrentTypeface;
        var boldTypeface = new Typeface(typeface.FontFamily, typeface.Style, FontWeights.Bold, typeface.Stretch);
        double fontSize = _editor.FontSize;
        double dpi = Dpi;

        // One visual line per logical line, however many rows wrapping gives it; the
        // number goes on its first row.
        foreach (var line in _textView.VisualLines)
        {
            int logical = line.FirstDocumentLine.LineNumber;
            double top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - _textView.VerticalOffset;
            double y = _textView.TranslatePoint(new Point(0, top), this).Y;

            bool isCurrent = logical == CurrentLine;
            var ft = new FormattedText(logical.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                isCurrent ? boldTypeface : typeface, fontSize,
                isCurrent ? _currentBrush : _numberBrush, dpi);

            dc.DrawText(ft, new Point(ActualWidth - RightPadding - ft.Width, y));
        }
    }
}
