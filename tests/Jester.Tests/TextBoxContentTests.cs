using System.Windows.Controls;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Reading part of a TextBox's text without its Text property. Every stretch read must
/// match the same stretch of Text, whatever edits came before.
/// </summary>
public class TextBoxContentTests
{
    [Fact]
    public void ReadsTheSameCharactersAsTheTextProperty() => StaThread.Run(() =>
    {
        var box = new TextBox();
        var content = new TextBoxContent(box);
        Assert.True(content.IsDirect);
        AssertMatches(box, content);

        box.Text = "one\r\ntwo\rthree\nfour";
        AssertMatches(box, content);

        box.Select(5, 0);
        box.SelectedText = "\u00e9\U0001F600\t";
        AssertMatches(box, content);

        box.Select(2, 6);
        box.SelectedText = "";
        AssertMatches(box, content);

        box.AppendText(new string('x', 10_000) + "\r\n");
        AssertMatches(box, content);

        box.Undo();
        AssertMatches(box, content);

        box.Clear();
        AssertMatches(box, content);
    });

    private static void AssertMatches(TextBox box, TextBoxContent content)
    {
        string text = box.Text;
        Assert.Equal(text.Length, content.Length);
        Assert.Equal(text, content.Read(0, text.Length));
        for (int start = 0; start <= text.Length; start += Math.Max(1, text.Length / 50))
        {
            for (int length = 0; start + length <= text.Length && length <= 5; length++)
                Assert.Equal(text.Substring(start, length), content.Read(start, length));
        }
        Assert.Equal("", content.Read(text.Length, 0));
    }
}
