using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Esc closes the open find panel from the text, but a menu that has the keyboard
/// (the menu bar after Alt or F10, an open submenu, a context menu) keeps Esc for itself.
/// </summary>
public class FindPanelEscapeTests
{
    [Fact]
    public void EscapeInTheTextClosesThePanel() => StaThread.Run(() =>
    {
        var text = new TextBox();
        new Grid().Children.Add(text);

        Assert.True(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: false, text));
        Assert.True(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: false, null));
    });

    [Fact]
    public void EscapeLeavesAClosedPanelAndTheResultsAlone() => StaThread.Run(() =>
    {
        var text = new TextBox();

        Assert.False(MainWindow.EscapeClosesFindPanel(panelVisible: false, resultsHaveFocus: false, text));
        Assert.False(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: true, text));
    });

    [Fact]
    public void EscapeInTheMenuBarIsLeftToTheMenu() => StaThread.Run(() =>
    {
        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        menu.Items.Add(file);

        Assert.False(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: false, file));
    });

    [Fact]
    public void EscapeInAnOpenSubmenuIsLeftToTheMenu() => StaThread.Run(() =>
    {
        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        var reopen = new MenuItem { Header = "Reopen with _Encoding" };
        var utf8 = new MenuItem { Header = "UTF-8" };
        reopen.Items.Add(utf8);
        file.Items.Add(reopen);
        menu.Items.Add(file);

        Assert.False(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: false, utf8));
    });

    [Fact]
    public void EscapeInAContextMenuIsLeftToTheMenu() => StaThread.Run(() =>
    {
        var context = new ContextMenu();
        var copy = new MenuItem { Header = "_Copy" };
        context.Items.Add(copy);

        Assert.False(MainWindow.EscapeClosesFindPanel(panelVisible: true, resultsHaveFocus: false, copy));
    });
}
