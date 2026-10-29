using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Edits the menus make to the editor's document. They go through the document, not
/// its Text property, so they keep the undo history, and Undo takes each back whole.
/// </summary>
public class EditorEditsTests
{
    [Theory]
    [InlineData("ab", 0, "b")]
    [InlineData("ab", 1, "a")]
    [InlineData("a\r\nb", 1, "ab")]
    [InlineData("a\rb", 1, "ab")]
    [InlineData("a\U0001F600b", 1, "ab")]
    [InlineData("ab", 2, "ab")]
    public void DeleteRemovesACrLfOrSurrogatePairWhole(string text, int caret, string expected)
    {
        var document = new TextDocument(text);
        MainWindow.DeleteNextChar(document, caret);
        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void DeleteFromTheEditorReachesTheWindow() => StaThread.Run(() =>
    {
        // The window's Delete also removes the next character; AvalonEdit's own would
        // only remove a selection, and would stop the command before the window.
        var view = new EditorView();
        var host = new Border { Child = view };
        bool reached = false;
        host.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (_, _) => reached = true));
        host.Measure(new Size(400, 300));
        host.Arrange(new Rect(0, 0, 400, 300));

        view.Editor.Text = "abc";
        view.Editor.Select(0, 2);
        ApplicationCommands.Delete.Execute(null, view.Editor.TextArea);

        Assert.True(reached);
        Assert.Equal("abc", view.Editor.Text);
    });

    [Fact]
    public void UndoTakesBackARunOfTypingAtOnce() => StaThread.Run(() =>
    {
        var ed = new EditorView().Editor;
        var area = ed.TextArea;
        ed.Text = "x";
        ed.Select(1, 0);
        foreach (char c in "abc")
            area.PerformTextInput(c.ToString());
        ed.Select(0, 0); // Moving the caret ends the run.
        area.PerformTextInput("y");
        area.PerformTextInput("z");
        ed.Document.Insert(ed.Document.TextLength, "-"); // So does any other edit.
        area.PerformTextInput("w");

        Assert.Equal("yzwxabc-", ed.Text);
        Assert.True(ed.Undo());
        Assert.Equal("yzxabc-", ed.Text);
        Assert.True(ed.Undo());
        Assert.Equal("yzxabc", ed.Text);
        Assert.True(ed.Undo());
        Assert.Equal("xabc", ed.Text);
        Assert.True(ed.Undo());
        Assert.Equal("x", ed.Text);
        Assert.False(ed.CanUndo);
    });

    [Fact]
    public void CtrlDDoesNotDeleteTheLine() => StaThread.Run(() =>
    {
        var view = new EditorView();
        view.Editor.Text = "one\ntwo";

        Assert.False(AvalonEditCommands.DeleteLine.CanExecute(null, view.Editor.TextArea));
        AvalonEditCommands.DeleteLine.Execute(null, view.Editor.TextArea);
        Assert.Equal("one\ntwo", view.Editor.Text);
    });

    [Theory]
    [InlineData("LF", "one\ntwo\nthree\n")]
    [InlineData("CR", "one\rtwo\rthree\r")]
    [InlineData("CRLF", "one\r\ntwo\r\nthree\r\n")]
    public void ConvertingLineEndingsKeepsTheCaretAndUndoesInOneStep(string kind, string expected) => StaThread.Run(() =>
    {
        const string original = "one\r\ntwo\nthree\r";
        var view = new EditorView();
        var ed = view.Editor;
        ed.Text = original;
        ed.Select(ed.Document.GetOffset(3, 4), 0); // After "thr".

        MainWindow.ConvertLineEndings(view, kind);

        Assert.Equal(expected, ed.Text);
        Assert.Equal(kind, view.Lines.LineEnding);
        Assert.Equal((3, 4), view.Lines.PositionOf(ed.CaretOffset));

        Assert.True(ed.Undo());
        Assert.Equal(original, ed.Text);
        Assert.False(ed.CanUndo);
    });

    [Theory]
    [InlineData("LF", "\n")]
    [InlineData("CR", "\r")]
    [InlineData("CRLF", "\r\n")]
    public void ALineEndingChosenForTextWithNoBreakIsKept(string kind, string lineBreak) => StaThread.Run(() =>
    {
        var view = new EditorView();
        var ed = view.Editor;
        ed.Text = "abc";

        MainWindow.ConvertLineEndings(view, kind);

        Assert.Equal("abc", ed.Text);
        Assert.False(ed.CanUndo);
        Assert.Equal(kind, view.Lines.LineEnding);
        Assert.Equal(lineBreak, MainWindow.LineBreakAt(view.Lines.LineEnding, "abc", 3, indent: false));
    });

    [Fact]
    public void TheTextsOwnBreaksComeBeforeTheChosenLineEnding() => StaThread.Run(() =>
    {
        var view = new EditorView();
        var document = view.Editor.Document;
        document.Text = "abc";
        MainWindow.ConvertLineEndings(view, "LF");

        document.Insert(1, "\r\n");
        Assert.Equal("CRLF", view.Lines.LineEnding);

        document.Remove(1, 2);
        Assert.Equal("LF", view.Lines.LineEnding);
    });

    [Theory]
    [InlineData("LF", "")]
    [InlineData("LF", "abc")]
    [InlineData("CR", "")]
    [InlineData("CR", "abc")]
    [InlineData("CRLF", "")]
    [InlineData("CRLF", "abc")]
    public void PastedLinesTakeTheLineEndingChosenForTextWithNoBreak(string kind, string start) => StaThread.Run(() =>
    {
        string lineBreak = MainWindow.LineBreakAt(kind, "", 0, indent: false);
        foreach (string pasted in new[] { "\n", "\r", "\r\n" })
        {
            foreach (bool dragDrop in new[] { false, true })
            {
                var view = new EditorView();
                var ed = view.Editor;
                ed.Text = start;
                ed.CaretOffset = start.Length;
                MainWindow.ConvertLineEndings(view, kind);

                Paste(ed.TextArea, "x" + pasted + "y", dragDrop);

                string inserted = "x" + lineBreak + "y";
                Assert.Equal(start + inserted, ed.Text);
                Assert.Equal(kind, view.Lines.LineEnding);
                Assert.Equal(dragDrop ? start.Length : ed.Text.Length, ed.SelectionStart);
                Assert.Equal(dragDrop ? inserted.Length : 0, ed.SelectionLength);

                Assert.True(ed.Undo());
                Assert.Equal(start, ed.Text);
                Assert.Equal(kind, view.Lines.LineEnding);
                Assert.False(ed.CanUndo);
            }
        }
    });

    [Fact]
    public void PastedLinesKeepTheBreaksOfATextThatHasThem() => StaThread.Run(() =>
    {
        var view = new EditorView();
        var ed = view.Editor;
        ed.Text = "a\nb";
        ed.CaretOffset = 3;
        MainWindow.ConvertLineEndings(view, "LF");

        Paste(ed.TextArea, "x\r\ny", dragDrop: false);

        Assert.Equal("a\nbx\ny", ed.Text);
    });

    // The editor's own paste and drop without the clipboard or a mouse: Pasting first, then its insert.
    private static void Paste(TextArea area, string text, bool dragDrop)
    {
        var e = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), dragDrop, DataFormats.UnicodeText);
        area.RaiseEvent(e);
        if (e.CommandCancelled)
            return;

        var document = area.Document;
        text = TextUtilities.NormalizeNewLines(text, TextUtilities.GetNewLineFromDocument(document, area.Caret.Line));
        if (dragDrop)
        {
            int offset = area.Caret.Offset;
            document.Insert(offset, text);
            area.Selection = Selection.Create(area, offset, offset + text.Length);
        }
        else
        {
            area.Selection.ReplaceSelectionWithText(text);
        }
    }

    [Fact]
    public void ConvertingToTheSameLineEndingChangesNothing() => StaThread.Run(() =>
    {
        var view = new EditorView();
        var ed = view.Editor;
        ed.Text = "one\ntwo";
        bool changed = false;
        ed.TextChanged += (_, _) => changed = true;

        MainWindow.ConvertLineEndings(view, "LF");

        Assert.False(changed);
        Assert.False(ed.CanUndo);
    });

    [Fact]
    public void ReplaceAllUndoesInOneStep() => StaThread.Run(() =>
    {
        var ed = new EditorView().Editor;
        ed.Text = "a cat, a Cat, a CAT";
        ed.Select(5, 0);

        Assert.Equal(3, MainWindow.ReplaceAllIn(ed, "cat", "dog", matchCase: false));

        Assert.Equal("a dog, a dog, a dog", ed.Text);
        Assert.Equal(5, ed.CaretOffset);
        Assert.True(ed.Undo());
        Assert.Equal("a cat, a Cat, a CAT", ed.Text);
        Assert.False(ed.CanUndo);
    });
}
