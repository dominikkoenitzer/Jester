using System.IO;
using System.Text;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Unsaved work survives a restart: what a tab keeps, how the sessions of several
/// windows come back as one, and when a file changed on disk under unsaved edits.
/// </summary>
public class SessionStateTests
{
    private static readonly string A = Path.Combine(Path.GetTempPath(), "jester_a.txt");
    private static readonly string B = Path.Combine(Path.GetTempPath(), "jester_b.txt");
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly DiskStamp Stamp = new(638_000_000_000_000_000, 42);

    private static SessionTab Saved(string path) => new() { Path = path };
    private static SessionTab Edited(string? path, string text) => new() { Path = path, Text = text };

    private static SessionSnapshot Session(int minute, int active, params SessionTab[] tabs) => new()
    {
        SavedUtc = new DateTime(2026, 9, 29, 12, minute, 0, DateTimeKind.Utc),
        ActiveTab = active,
        Tabs = tabs.ToList(),
    };

    // ---------------------------------------------------------------- capture

    [Fact]
    public void ABlankUntitledTabIsNotKept()
    {
        Assert.Null(SessionState.Capture(null, "", isDirty: false, 0, Utf8, null));
    }

    [Fact]
    public void AnUntitledTabKeepsItsTextAndCaret()
    {
        var tab = SessionState.Capture(null, "draft\r\nline", isDirty: true, 5, Utf8, null)!;

        Assert.Null(tab.Path);
        Assert.Equal("draft\r\nline", tab.Text);
        Assert.Equal(5, tab.Caret);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void ASavedFileKeepsOnlyItsPath()
    {
        var tab = SessionState.Capture(A, "on disk", isDirty: false, 3, Utf8, Stamp)!;

        Assert.Equal(A, tab.Path);
        Assert.Null(tab.Text);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void AFileWithUnsavedEditsKeepsTheEditsAndTheStampTheyWereMadeAgainst()
    {
        var tab = SessionState.Capture(A, "edited", isDirty: true, 6, Utf8, Stamp)!;

        Assert.Equal(A, tab.Path);
        Assert.Equal("edited", tab.Text);
        Assert.Equal(Stamp, tab.Disk);
    }

    [Fact]
    public void KeepsTheLineEndingsExactly()
    {
        var tab = SessionState.Capture(null, "a\nb\rc\r\n", isDirty: true, 0, Utf8, null)!;

        Assert.Equal("a\nb\rc\r\n", tab.Text);
    }

    [Fact]
    public void ACaretPastTheEndIsClamped()
    {
        Assert.Equal(3, SessionState.Capture(null, "abc", isDirty: true, 99, Utf8, null)!.Caret);
    }

    // --------------------------------------------------------------- snapshot

    [Fact]
    public void TheActiveTabCountsOnlyTheTabsKept()
    {
        var kept = Edited(null, "x");
        var snapshot = SessionState.Snapshot(new SessionTab?[] { null, Saved(A), kept }, 2, DateTime.UtcNow);

        Assert.Equal(2, snapshot.Tabs.Count);
        Assert.Equal(1, snapshot.ActiveTab);
    }

    [Fact]
    public void ABlankActiveTabPassesTheFocusToTheNextTab()
    {
        var snapshot = SessionState.Snapshot(new SessionTab?[] { Saved(A), null, Saved(B) }, 1, DateTime.UtcNow);

        Assert.Equal(1, snapshot.ActiveTab);
    }

    [Fact]
    public void ABlankLastTabPassesTheFocusBack()
    {
        var snapshot = SessionState.Snapshot(new SessionTab?[] { Saved(A), null }, 1, DateTime.UtcNow);

        Assert.Equal(0, snapshot.ActiveTab);
    }

    // ------------------------------------------------------------------ merge

    [Fact]
    public void OneSessionComesBackAsItWas()
    {
        var tabs = new[] { Saved(A), Edited(null, "note"), Edited(B, "changed") };

        var merged = SessionState.Merge(new[] { Session(0, 2, tabs) });

        Assert.Equal(tabs, merged.Tabs);
        Assert.Equal(2, merged.ActiveTab);
    }

    [Fact]
    public void SeveralSessionsComeBackOldestFirstWithTheNewestActiveTab()
    {
        var older = Edited(null, "older");
        var newer = Edited(null, "newer");

        var merged = SessionState.Merge(new[] { Session(5, 0, newer), Session(1, 0, older) });

        Assert.Equal(new[] { older, newer }, merged.Tabs);
        Assert.Equal(1, merged.ActiveTab);
    }

    [Fact]
    public void AFileOpenInTwoSessionsComesBackOnce()
    {
        var merged = SessionState.Merge(new[] { Session(1, 0, Saved(A)), Session(2, 0, Saved(A), Saved(B)) });

        Assert.Equal(new[] { A, B }, merged.Tabs.Select(t => t.Path));
        Assert.Equal(0, merged.ActiveTab);
    }

    [Fact]
    public void UnsavedEditsWinOverTheSavedCopyOfTheSameFile()
    {
        var edits = Edited(A, "edits");

        var merged = SessionState.Merge(new[] { Session(1, 0, Saved(A)), Session(2, 0, edits) });

        Assert.Equal(new[] { edits }, merged.Tabs);
    }

    [Fact]
    public void TwoSetsOfEditsToOneFileAreBothKept()
    {
        var older = Edited(A, "older edits");
        var newer = Edited(A, "newer edits");

        var merged = SessionState.Merge(new[] { Session(2, 0, newer), Session(1, 0, older) });

        Assert.Equal(2, merged.Tabs.Count);
        Assert.Null(merged.Tabs[0].Path);
        Assert.Equal("older edits", merged.Tabs[0].Text);
        Assert.Same(newer, merged.Tabs[1]);
    }

    [Fact]
    public void NoSessionsMeanNoTabs()
    {
        Assert.Empty(SessionState.Merge(Array.Empty<SessionSnapshot>()).Tabs);
    }

    // ------------------------------------------------------ changed on disk

    [Fact]
    public void AnUntouchedFileHasNotChanged()
    {
        var tab = Edited(A, "edits");
        tab.Disk = Stamp;

        Assert.False(SessionState.ChangedOnDisk(tab, Stamp with { }));
    }

    [Fact]
    public void ANewerWriteOrOtherSizeIsAChange()
    {
        var tab = Edited(A, "edits");
        tab.Disk = Stamp;

        Assert.True(SessionState.ChangedOnDisk(tab, Stamp with { LastWriteUtcTicks = Stamp.LastWriteUtcTicks + 1 }));
        Assert.True(SessionState.ChangedOnDisk(tab, Stamp with { Length = 43 }));
    }

    [Fact]
    public void ADeletedFileIsAChange()
    {
        var tab = Edited(A, "edits");
        tab.Disk = Stamp;

        Assert.True(SessionState.ChangedOnDisk(tab, null));
    }

    [Fact]
    public void ASavedFileOrUntitledTabNeverConflicts()
    {
        var saved = Saved(A);
        saved.Disk = Stamp;

        Assert.False(SessionState.ChangedOnDisk(saved, null));
        Assert.False(SessionState.ChangedOnDisk(Edited(null, "x"), Stamp));
    }

    // ---------------------------------------------------------- active tab

    [Fact]
    public void RestoresTheActiveTab()
    {
        Assert.Equal(2, SessionState.RestoredActive(new[] { true, true, true }, 2));
    }

    [Fact]
    public void AnActiveTabThatFailedToOpenPassesTheFocusOn()
    {
        Assert.Equal(1, SessionState.RestoredActive(new[] { true, false, true }, 1));
        Assert.Equal(0, SessionState.RestoredActive(new[] { true, false }, 1));
        Assert.Equal(0, SessionState.RestoredActive(new[] { false, false }, 1));
    }

    // ------------------------------------------------------------- encoding

    [Theory]
    [InlineData("utf-8", false)]
    [InlineData("utf-8", true)]
    [InlineData("utf-16LE", true)]
    [InlineData("utf-16BE", true)]
    public void AnEncodingRoundTrips(string name, bool bom)
    {
        Encoding original = name switch
        {
            "utf-16LE" => new UnicodeEncoding(bigEndian: false, byteOrderMark: bom),
            "utf-16BE" => new UnicodeEncoding(bigEndian: true, byteOrderMark: bom),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom),
        };

        var (codePage, keptBom) = SessionState.Describe(original);
        var restored = SessionState.ToEncoding(codePage, keptBom);

        Assert.Equal(original.CodePage, restored.CodePage);
        Assert.Equal(original.GetPreamble(), restored.GetPreamble());
    }

    [Fact]
    public void AnAnsiCodePageRoundTrips()
    {
        var restored = SessionState.ToEncoding(1252, bom: false);

        Assert.Equal(1252, restored.CodePage);
        Assert.Equal(new byte[] { 0xE9 }, restored.GetBytes("é"));
    }

    [Fact]
    public void AnUnknownCodePageFallsBackToUtf8()
    {
        var restored = SessionState.ToEncoding(-5, bom: false);

        Assert.Equal(65001, restored.CodePage);
        Assert.Empty(restored.GetPreamble());
    }
}
