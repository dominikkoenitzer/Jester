using System.IO;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Startup: the tabs from the last session come back, and a file passed on the
/// command line ("Open with Jester") opens alongside them rather than instead.
/// Closing saves every open file as the next session, so a file list that
/// dropped the session tabs would lose them for good.
/// </summary>
public class SessionTests
{
    private static readonly string A = Path.Combine(Path.GetTempPath(), "jester_a.txt");
    private static readonly string B = Path.Combine(Path.GetTempPath(), "jester_b.txt");
    private static readonly string C = Path.Combine(Path.GetTempPath(), "jester_c.txt");

    [Fact]
    public void RestoresTheSessionWhenThereIsNoCommandLineFile()
    {
        Assert.Equal(new[] { A, B }, MainWindow.StartupFiles(new[] { A, B }, Array.Empty<string>()));
    }

    [Fact]
    public void OpensACommandLineFileAfterTheSessionTabs()
    {
        Assert.Equal(new[] { A, B, C }, MainWindow.StartupFiles(new[] { A, B }, new[] { C }));
    }

    [Fact]
    public void DoesNotOpenAFileTwiceWhenItIsAlreadyInTheSession()
    {
        Assert.Equal(new[] { A, B }, MainWindow.StartupFiles(new[] { A, B }, new[] { B.ToUpperInvariant() }));
    }

    // ------------------------------------------------------------ active tab

    // The session only keeps tabs that have a file, so the saved active tab has to
    // count those, not every tab including the Untitled ones.

    [Fact]
    public void SavesTheActiveTabAsAPositionAmongTheSavedFiles()
    {
        string?[] tabs = { null, A, null, B };

        Assert.Equal(1, MainWindow.SessionActiveTab(tabs, 3));
    }

    [Fact]
    public void SavesTheFirstTabWhenNothingIsSelected()
    {
        Assert.Equal(0, MainWindow.SessionActiveTab(new[] { A, B }, -1));
    }

    [Fact]
    public void AnUntitledActiveTabSavesTheNextFileAlongIt()
    {
        string?[] tabs = { null, A, null, B };

        Assert.Equal(1, MainWindow.SessionActiveTab(tabs, 2));
    }

    [Fact]
    public void RestoresTheSavedActiveFile()
    {
        Assert.Equal(1, MainWindow.RestoredActiveTab(new[] { A, B, C }, new[] { A, B, C }, 1));
    }

    [Fact]
    public void RestoresTheSavedActiveFileWhenAnEarlierOneIsGone()
    {
        // A no longer exists, so the restored tabs are B and C. B was active.
        Assert.Equal(0, MainWindow.RestoredActiveTab(new[] { B, C }, new[] { A, B, C }, 1));
    }

    [Fact]
    public void FallsBackToTheNearestTabWhenTheSavedIndexIsOutOfRange()
    {
        Assert.Equal(1, MainWindow.RestoredActiveTab(new[] { A, B }, new[] { A, B }, 5));
        Assert.Equal(0, MainWindow.RestoredActiveTab(new[] { A, B }, new[] { A, B }, -3));
    }
}
