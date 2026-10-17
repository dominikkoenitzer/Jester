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
    private const double LeftPadding = 16;
    private const double RightPadding = 12;

    // The theme's colours for the numbers and for the caret's line.
    private static readonly DependencyProperty NumberBrushProperty =
        DependencyProperty.Register("NumberBrush", typeof(Brush), typeof(LineNumberMargin),
            new FrameworkPropertyMetadata(Frozen(Color.FromRgb(0xB2, 0xAA, 0xBD)), FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty CurrentBrushProperty =
        DependencyProperty.Register("CurrentBrush", typeof(Brush), typeof(LineNumberMargin),
            new FrameworkPropertyMetadata(Frozen(Color.FromRgb(0xA8, 0x79, 0x0F)), FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly TextEditor _editor;
    private readonly TextView _textView;

    /// <summary>Total logical lines in the document; drives the gutter width.</summary>
    public int TotalLines { get; set; } = 1;

    /// <summary>1-based logical line the caret sits on; drawn in the accent colour.</summary>
    public int CurrentLine { get; set; } = 1;

    public LineNumberMargin(TextEditor editor)
    {
        _editor = editor;
        _textView = editor.TextArea.TextView;

        SetResourceReference(NumberBrushProperty, "GutterTextBrush");
        SetResourceReference(CurrentBrushProperty, "GutterCurrentBrush");

        // A number half scrolled off the top stays inside the gutter, not over the tabs.
        ClipToBounds = true;

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
            FlowDirection.LeftToRight, CurrentTypeface, _editor.FontSize, Brushes.Black, Dpi);

        double width = LeftPadding + sample.Width + RightPadding;
        double height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Before its template is applied the view is not yet beside the gutter.
        if (!_textView.VisualLinesValid || FindCommonVisualAncestor(_textView) is null)
            return;

        var typeface = CurrentTypeface;
        var numberBrush = (Brush)GetValue(NumberBrushProperty);
        var currentBrush = (Brush)GetValue(CurrentBrushProperty);
        double fontSize = _editor.FontSize;
        double dpi = Dpi;

        // One visual line per logical line, however many rows wrapping gives it; the
        // number goes on its first row.
        foreach (var line in _textView.VisualLines)
        {
            // A line whose first row has scrolled out of sight is not numbered, just like
            // a row that only continues a wrapped line.
            var firstRow = line.TextLines[0];
            if (line.GetTextLineVisualYPosition(firstRow, VisualYPosition.LineBottom) <= _textView.VerticalOffset)
                continue;

            int logical = line.FirstDocumentLine.LineNumber;
            double top = line.GetTextLineVisualYPosition(firstRow, VisualYPosition.TextTop) - _textView.VerticalOffset;
            double y = _textView.TranslatePoint(new Point(0, top), this).Y;

            bool isCurrent = logical == CurrentLine;
            var ft = new FormattedText(logical.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, fontSize, isCurrent ? currentBrush : numberBrush, dpi);

            dc.DrawText(ft, new Point(ActualWidth - RightPadding - ft.Width, y));
        }
    }
}
