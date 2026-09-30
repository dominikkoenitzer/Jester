using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
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

        MainWindow.ConvertLineEndings(ed, kind);

        Assert.Equal(expected, ed.Text);
        Assert.Equal(kind, view.Lines.LineEnding);
        Assert.Equal((3, 4), view.Lines.PositionOf(ed.CaretOffset));

        Assert.True(ed.Undo());
        Assert.Equal(original, ed.Text);
        Assert.False(ed.CanUndo);
    });

    [Fact]
    public void ConvertingToTheSameLineEndingChangesNothing() => StaThread.Run(() =>
    {
        var ed = new EditorView().Editor;
        ed.Text = "one\ntwo";
        bool changed = false;
        ed.TextChanged += (_, _) => changed = true;

        MainWindow.ConvertLineEndings(ed, "LF");

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
