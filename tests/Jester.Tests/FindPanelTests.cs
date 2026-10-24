using System.Windows;
using ICSharpCode.AvalonEdit;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// The find panel as Ctrl+F, Ctrl+H and F3 leave it. The panel is built without the
/// app's resources, so its close button style is looked up dynamically.
/// </summary>
public class FindPanelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CtrlFOnAnOpenPanelKeepsTheOptionsTickedThere(bool replaceMode) => StaThread.Run(() =>
    {
        var bar = new FindPanel { Visibility = Visibility.Visible, MatchCase = true, WrapAround = false };

        MainWindow.PrepareFindPanel(bar, new TextEditor(), replaceMode, "cat", matchCase: false, wrapAround: true);

        Assert.True(bar.MatchCase);
        Assert.False(bar.WrapAround);
    });

    [Fact]
    public void OpeningThePanelTakesTheLastSearchAndItsOptions() => StaThread.Run(() =>
    {
        var bar = new FindPanel { Visibility = Visibility.Collapsed };

        MainWindow.PrepareFindPanel(bar, new TextEditor(), false, "cat", matchCase: true, wrapAround: false);

        Assert.Equal("cat", bar.SearchText);
        Assert.True(bar.MatchCase);
        Assert.False(bar.WrapAround);
    });

    [Fact]
    public void AMatchFromF3ClearsAnOldNoResults() => StaThread.Run(() =>
    {
        var bar = new FindPanel { Visibility = Visibility.Visible };
        bar.ShowStatus("No results");

        Assert.True(MainWindow.ReportInFindPanel(bar, found: true));
        Assert.Equal("", bar.StatusText.Text);
    });

    [Fact]
    public void AMissFromF3ShowsInTheOpenPanel() => StaThread.Run(() =>
    {
        var bar = new FindPanel { Visibility = Visibility.Visible };

        Assert.True(MainWindow.ReportInFindPanel(bar, found: false));
        Assert.Equal("No results", bar.StatusText.Text);
    });

    [Fact]
    public void AMissFromF3WithThePanelClosedIsLeftToTheWindow() => StaThread.Run(() =>
    {
        var bar = new FindPanel { Visibility = Visibility.Collapsed };

        Assert.False(MainWindow.ReportInFindPanel(bar, found: false));
        Assert.Equal("", bar.StatusText.Text);
    });
}
