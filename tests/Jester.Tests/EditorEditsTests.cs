using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
}
