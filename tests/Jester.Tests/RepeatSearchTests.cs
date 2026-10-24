using Xunit;

namespace Jester.Tests;

/// <summary>What F3 and Shift+F3 search for.</summary>
public class RepeatSearchTests
{
    [Fact]
    public void TheOpenPanelsTextAndOptionsWin()
    {
        var found = MainWindow.RepeatSearch(panel: ("foo", true, false), last: ("bar", false, true));
        Assert.Equal(("foo", true, false), found);
    }

    [Fact]
    public void TheLastSearchWhenThePanelIsClosed()
    {
        var found = MainWindow.RepeatSearch(panel: null, last: ("bar", false, true));
        Assert.Equal(("bar", false, true), found);
    }

    [Fact]
    public void TheLastSearchWhenThePanelsBoxIsEmpty()
    {
        var found = MainWindow.RepeatSearch(panel: ("", true, false), last: ("bar", false, true));
        Assert.Equal(("bar", false, true), found);
    }
}
