using System.Windows.Threading;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// When the editor colours its text: only for a known language, only while the switch
/// is on, and not while any line is too long to colour quickly. These use a real
/// TextEditor with no window.
/// </summary>
public class EditorViewHighlightingTests
{
    private static string LongLine => new('x', EditorView.MaxHighlightedLineLength + 1);

    [Fact]
    public void AKnownLanguageIsColouredAndPlainTextIsNot() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = "fn main() {}";
        Assert.False(view.IsHighlighted);

        view.Language = SyntaxLanguages.DefinitionFor("main.rs");
        Assert.True(view.IsHighlighted);

        view.Language = SyntaxLanguages.DefinitionFor("notes.txt");
        Assert.Null(view.Language);
        Assert.False(view.IsHighlighted);
    });

    [Fact]
    public void TheSwitchTurnsTheColoursOffAndOn() => StaThread.Run(() =>
    {
        var view = new EditorView { Language = SyntaxLanguages.Definition("Rust") };

        view.HighlightingEnabled = false;
        Assert.False(view.IsHighlighted);
        Assert.NotNull(view.Language);

        view.HighlightingEnabled = true;
        Assert.True(view.IsHighlighted);
    });

    [Fact]
    public void TheSwitchHoldsForALanguageSetLater() => StaThread.Run(() =>
    {
        var view = new EditorView { HighlightingEnabled = false };
        view.Language = SyntaxLanguages.Definition("Rust");
        Assert.False(view.IsHighlighted);
    });

    [Fact]
    public void AFileWithAVeryLongLineOpensUncoloured() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = "fn main() {}\n" + LongLine;
        view.Language = SyntaxLanguages.Definition("Rust");
        Assert.False(view.IsHighlighted);
    });

    [Fact]
    public void TypingALineTooLongTurnsTheColoursOffAndShorteningItBringsThemBack() => StaThread.Run(() =>
    {
        var view = new EditorView { Language = SyntaxLanguages.Definition("Rust") };
        var ed = view.Editor;
        ed.Text = "fn main() {}\nlet x = 1;";
        Assert.True(view.IsHighlighted);

        ed.Document.Insert(ed.Document.TextLength, LongLine);
        Assert.False(view.IsHighlighted);

        ed.Document.Remove(ed.Document.TextLength - 20, 20);
        Assert.True(view.IsHighlighted);
    });

    [Fact]
    public void OpeningAnotherFileBringsTheColoursBack() => StaThread.Run(() =>
    {
        var view = new EditorView { Language = SyntaxLanguages.Definition("Rust") };
        view.Editor.Text = LongLine;
        Assert.False(view.IsHighlighted);

        view.Editor.Text = "fn main() {}";
        Assert.True(view.IsHighlighted);
    });

    [Fact]
    public void ReadingAheadReachesTheLastLine() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = string.Concat(Enumerable.Repeat("let s = \"a\"; // note\n", 5000));
        view.Language = SyntaxLanguages.Definition("Rust");

        Pump(() => view.WarmedLines >= view.Editor.Document.LineCount);
        Assert.Equal(view.Editor.Document.LineCount, view.WarmedLines);
    });

    [Fact]
    public void AnEditReadsAheadAgainFromItsLine() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = string.Concat(Enumerable.Repeat("let x = 1;\n", 3000));
        view.Language = SyntaxLanguages.Definition("Rust");
        Pump(() => view.WarmedLines >= view.Editor.Document.LineCount);

        // Opening a comment near the top changes how every later line starts.
        view.Editor.Document.Insert(view.Editor.Document.GetLineByNumber(10).Offset, "/*");
        Assert.True(view.WarmedLines < 10);

        Pump(() => view.WarmedLines >= view.Editor.Document.LineCount);
        Assert.Equal(view.Editor.Document.LineCount, view.WarmedLines);
    });

    // Runs the dispatcher until the condition holds, or five seconds have passed.
    private static void Pump(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < until)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.SystemIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }
}
