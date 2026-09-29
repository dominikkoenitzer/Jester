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
}
