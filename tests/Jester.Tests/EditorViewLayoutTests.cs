using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// The gutter and the current-line highlight, laid out with no window. Each line gets
/// one number, a lone CR included and however many rows wrapping gives it, and the
/// highlight covers the caret's row across the whole width of the text.
/// </summary>
public class EditorViewLayoutTests
{
    [Theory]
    [InlineData("one\rtwo\rthree")]
    [InlineData("one\ntwo\r\nthree")]
    [InlineData("one\r\ntwo\r\nthree")]
    public void TheGutterNumbersEveryLine(string text) => StaThread.Run(() =>
    {
        var view = Shown(text, wrap: false);
        Assert.Equal(["1", "2", "3"], GutterNumbers(view));

        // Each number sits on the baseline of its line's text.
        var lines = view.Editor.TextArea.TextView.VisualLines;
        var baselines = GutterBaselines(view);
        for (int i = 0; i < 3; i++)
        {
            double textBaseline = view.Editor.Padding.Top + lines[i].VisualTop + lines[i].TextLines[0].Baseline;
            Assert.Equal(textBaseline, baselines[i], 1);
        }
    });

    [Fact]
    public void TheGutterNumbersAWrappedLineOnce() => StaThread.Run(() =>
    {
        var view = Shown(new string('x', 400) + "\nend", wrap: true);

        Assert.True(view.Editor.TextArea.TextView.VisualLines[0].TextLines.Count > 1);
        Assert.Equal(["1", "2"], GutterNumbers(view));
    });

    [Fact]
    public void TheGutterLeavesALineUnnumberedOnceItsFirstRowScrollsAway() => StaThread.Run(() =>
    {
        var view = Shown(new string('x', 400) + "\nend" + string.Concat(Enumerable.Repeat("\nmore", 30)), wrap: true);
        var textView = view.Editor.TextArea.TextView;
        double firstRow = textView.VisualLines[0].TextLines[0].Height;

        // The scroll viewer leaves its scrolling to a window; the view scrolls at once.
        ((IScrollInfo)textView).SetVerticalOffset(firstRow * 2);
        view.UpdateLayout();

        Assert.Equal(firstRow * 2, textView.VerticalOffset, 3);
        Assert.DoesNotContain("1", GutterNumbers(view));
        Assert.Contains("2", GutterNumbers(view));
    });

    [Fact]
    public void TheCurrentLineHighlightCoversTheCaretsRow() => StaThread.Run(() =>
    {
        var view = Shown("one\ntwo\nthree", wrap: false);
        view.Editor.Select(5, 0);

        var textArea = (Grid)view.Children[1];
        var highlight = (Rectangle)((Canvas)textArea.Children[0]).Children[0];
        var row = view.Editor.TextArea.TextView.VisualLines[1];
        Assert.Equal(Visibility.Visible, highlight.Visibility);
        Assert.Equal(view.Editor.Padding.Top + row.VisualTop, Canvas.GetTop(highlight), 3);
        Assert.Equal(row.Height, highlight.Height, 3);
        Assert.Equal(textArea.ActualWidth, highlight.Width, 3);
    });

    private static EditorView Shown(string text, bool wrap)
    {
        var view = new EditorView();
        view.Editor.Text = text;
        view.Editor.WordWrap = wrap;
        var host = new Border { Child = view };
        host.Measure(new Size(400, 300));
        host.Arrange(new Rect(0, 0, 400, 300));
        host.UpdateLayout();
        return view;
    }

    // The numbers the gutter drew last, top to bottom.
    private static List<string> GutterNumbers(EditorView view) =>
        GutterGlyphs(view).Select(run => new string(run.Characters.ToArray())).ToList();

    private static List<double> GutterBaselines(EditorView view) =>
        GutterGlyphs(view).Select(run => run.BaselineOrigin.Y).ToList();

    private static List<GlyphRun> GutterGlyphs(EditorView view)
    {
        var margin = view.Children.OfType<LineNumberMargin>().Single();
        var runs = new List<GlyphRun>();
        void Walk(Drawing? drawing)
        {
            if (drawing is DrawingGroup group)
            {
                foreach (var child in group.Children)
                    Walk(child);
            }
            else if (drawing is GlyphRunDrawing glyphs)
            {
                runs.Add(glyphs.GlyphRun);
            }
        }
        Walk(VisualTreeHelper.GetDrawing(margin));
        return runs;
    }
}
