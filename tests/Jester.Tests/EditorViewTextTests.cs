using Xunit;

namespace Jester.Tests;

/// <summary>
/// The editor keeps its line index in step with the document from the changes the
/// document reports. These edit a real TextEditor, with no window, and check that the
/// index agrees with a full scan after typing, pasting, deleting, undo and redo.
/// </summary>
public class EditorViewTextTests
{
    [Fact]
    public void TheLineIndexFollowsEveryKindOfEdit() => StaThread.Run(() =>
    {
        var view = new EditorView();
        var ed = view.Editor;
        AssertInStep(view);

        ed.Text = "one\r\ntwo\rthree\nfour";
        AssertInStep(view);

        void Type(int at, string text)
        {
            ed.Select(at, 0);
            ed.SelectedText = text;
            AssertInStep(view);
        }

        void Replace(int at, int length, string text)
        {
            ed.Select(at, length);
            ed.SelectedText = text;
            AssertInStep(view);
        }

        Type(0, "x");
        Type(ed.Text.Length, "\n");
        Type(5, "\n");           // Inside the CRLF pair.
        Replace(4, 2, "");       // Takes the CR and the LF typed after it.
        Type(4, "\r\n\r\n");
        Replace(0, ed.Text.Length / 2, "a\rb");
        Type(ed.Text.Length, "\r");
        Type(ed.Text.Length, "\n");

        while (ed.CanUndo)
        {
            ed.Undo();
            AssertInStep(view);
        }
        while (ed.CanRedo)
        {
            ed.Redo();
            AssertInStep(view);
        }

        ed.Clear();
        AssertInStep(view);
    });

    [Fact]
    public void ReadsTheSameCharactersAsTheText() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = "one\r\ntwo\rthree\nfour";
        view.Editor.Select(5, 0);
        view.Editor.SelectedText = "\u00e9\U0001F600\t" + new string('x', 10_000);

        string text = view.Editor.Text;
        Assert.Equal(text, view.Read(0, text.Length));
        for (int start = 0; start <= text.Length; start += Math.Max(1, text.Length / 50))
        {
            for (int length = 0; start + length <= text.Length && length <= 5; length++)
                Assert.Equal(text.Substring(start, length), view.Read(start, length));
        }
        Assert.Equal("", view.Read(text.Length, 0));
    });

    private static void AssertInStep(EditorView view)
    {
        string text = view.Editor.Text;
        var fresh = new LineIndex(text);
        Assert.Equal(text.Length, view.Lines.Length);
        Assert.Equal(fresh.Count, view.Lines.Count);
        Assert.Equal(fresh.LineEnding, view.Lines.LineEnding);
        for (int i = 0; i <= text.Length; i++)
            Assert.Equal(fresh.PositionOf(i), view.Lines.PositionOf(i));
    }
}
