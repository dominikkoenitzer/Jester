using System.Text;
using Xunit;

namespace Jester.Tests;

/// <summary>Picking an encoding from the Encoding menu marks the tab unsaved only when the encoding changes.</summary>
public class EncodingChoiceTests
{
    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    public void PickingTheCurrentEncodingLeavesTheTabClean(string kind) => StaThread.Run(() =>
    {
        var tab = new DocumentTab();
        MainWindow.SetEncoding(tab, kind);
        tab.IsDirty = false;

        Assert.False(MainWindow.SetEncoding(tab, kind));
        Assert.False(tab.IsDirty);
        Assert.Equal(kind, MainWindow.EncodingKey(tab.Encoding));
    });

    [Fact]
    public void PickingAnotherEncodingMarksTheTabUnsaved() => StaThread.Run(() =>
    {
        var tab = new DocumentTab();

        Assert.True(MainWindow.SetEncoding(tab, "utf-16le"));
        Assert.True(tab.IsDirty);
        Assert.Equal("utf-16le", MainWindow.EncodingKey(tab.Encoding));
    });

    [Fact]
    public void AnAnsiFileTurnedIntoUtf8IsUnsaved() => StaThread.Run(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var tab = new DocumentTab { Encoding = Encoding.GetEncoding(1252) };

        Assert.True(MainWindow.SetEncoding(tab, "utf-8"));
        Assert.True(tab.IsDirty);
    });

    [Fact]
    public void UnsavedEditsStayUnsavedWhenTheEncodingStays() => StaThread.Run(() =>
    {
        var tab = new DocumentTab { IsDirty = true };

        MainWindow.SetEncoding(tab, "utf-8");

        Assert.True(tab.IsDirty);
    });
}
