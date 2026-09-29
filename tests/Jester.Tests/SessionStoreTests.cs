using System.IO;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// The session files on disk. Each window writes only its own file, a window that
/// is still running keeps its session, and one that is gone hands its tabs to the
/// next window that starts, without a moment where they exist nowhere.
/// </summary>
public class SessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jester_tests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static SessionSnapshot Session(string text) => new()
    {
        SavedUtc = DateTime.UtcNow,
        Tabs = { new SessionTab { Text = text } },
    };

    private static bool Write(SessionStore store, SessionSnapshot snapshot) => store.Write(snapshot, store.NextSequence());

    [Fact]
    public void AFreshInstallHasNoSessions()
    {
        using var store = new SessionStore(_dir);

        Assert.False(store.HadSessions);
        Assert.Empty(store.ClaimOrphans());
    }

    [Fact]
    public void AClosedWindowsSessionComesBackInTheNextOne()
    {
        using (var first = new SessionStore(_dir))
            Assert.True(Write(first, Session("keep me")));

        using var second = new SessionStore(_dir);
        var claimed = second.ClaimOrphans();

        Assert.True(second.HadSessions);
        Assert.Equal("keep me", Assert.Single(Assert.Single(claimed).Tabs).Text);
    }

    [Fact]
    public void ARunningWindowKeepsItsSession()
    {
        using var first = new SessionStore(_dir);
        Assert.True(Write(first, Session("mine")));

        using var second = new SessionStore(_dir);

        Assert.Empty(second.ClaimOrphans());
    }

    [Fact]
    public void TwoWindowsWriteTwoFiles()
    {
        using var first = new SessionStore(_dir);
        using var second = new SessionStore(_dir);

        Assert.True(Write(first, Session("one")));
        Assert.True(Write(second, Session("two")));

        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal(2, Directory.GetFiles(_dir, "*.json").Length);
    }

    [Fact]
    public void AClaimedSessionStaysOnDiskUntilTheNewSessionIsWritten()
    {
        using (var first = new SessionStore(_dir))
            Write(first, Session("old"));
        string oldPath = Directory.GetFiles(_dir, "*.json").Single();

        using var second = new SessionStore(_dir);
        second.ClaimOrphans();
        Assert.True(File.Exists(oldPath));

        Assert.True(Write(second, Session("old")));
        Assert.False(File.Exists(oldPath));
        Assert.Equal(second.Path, Directory.GetFiles(_dir, "*.json").Single());
    }

    [Fact]
    public void TwoWindowsStartingTogetherDoNotBothTakeTheSameSession()
    {
        using (var first = new SessionStore(_dir))
            Write(first, Session("once"));

        using var second = new SessionStore(_dir);
        using var third = new SessionStore(_dir);

        Assert.Single(second.ClaimOrphans());
        Assert.Empty(third.ClaimOrphans());
    }

    [Fact]
    public void ASessionThatCannotBeReadIsLeftAlone()
    {
        Directory.CreateDirectory(_dir);
        string bad = Path.Combine(_dir, "broken.json");
        File.WriteAllText(bad, "{ not json");

        using var store = new SessionStore(_dir);
        Assert.Empty(store.ClaimOrphans());
        Write(store, Session("x"));

        Assert.True(File.Exists(bad));
    }

    [Fact]
    public void AnOlderSnapshotNeverOverwritesANewerOne()
    {
        using var store = new SessionStore(_dir);
        long older = store.NextSequence();
        long newer = store.NextSequence();

        Assert.True(store.Write(Session("newer"), newer));
        Assert.True(store.Write(Session("older"), older));

        Assert.Contains("newer", File.ReadAllText(store.Path));
    }

    [Fact]
    public void AWriteLeavesNoTempFiles()
    {
        using var store = new SessionStore(_dir);
        Write(store, Session("a"));
        Write(store, Session("b"));

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void KeepsTheTextExactly()
    {
        const string text = "caf\u00E9 \U0001F600\r\nline\ttwo\rthree\n\"quoted\"";
        using (var first = new SessionStore(_dir))
            Write(first, Session(text));

        using var second = new SessionStore(_dir);

        Assert.Equal(text, second.ClaimOrphans().Single().Tabs.Single().Text);
    }

    [Fact]
    public void KeepsTheDiskStamp()
    {
        var stamp = new DiskStamp(638_000_000_000_000_000, 42);
        using (var first = new SessionStore(_dir))
        {
            var snapshot = Session("x");
            snapshot.Tabs[0].Path = @"C:\notes.txt";
            snapshot.Tabs[0].Disk = stamp;
            Write(first, snapshot);
        }

        using var second = new SessionStore(_dir);

        Assert.Equal(stamp, second.ClaimOrphans().Single().Tabs.Single().Disk);
    }
}
