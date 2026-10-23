using System.IO;
using System.Text;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Saving a document, the session and the settings through a temp file. A crash, a
/// power cut or a failed replace must leave either the old file or the new one.
/// </summary>
public class SaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jester_tests_" + Guid.NewGuid().ToString("N"));

    public SaveTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("utf-8", "")]
    [InlineData("utf-8", "Grüße\r\nzwei")]
    [InlineData("utf-8-bom", "")]
    [InlineData("utf-8-bom", "Grüße\r\nzwei")]
    [InlineData("utf-16le", "Grüße\r\nzwei")]
    [InlineData("utf-16be", "Grüße\r\nzwei")]
    [InlineData("windows-1252", "Grüße\r\nzwei")]
    public void WritesTheSameBytesAsWriteAllText(string kind, string text)
    {
        var encoding = MainWindow.ReopenEncoding(kind);
        string expected = Path.Combine(_dir, "expected.txt");
        string actual = Path.Combine(_dir, "actual.txt");

        File.WriteAllText(expected, text, encoding);
        FileOps.Real.WriteText(actual, text, encoding);

        Assert.Equal(File.ReadAllBytes(expected), File.ReadAllBytes(actual));
    }

    [Fact]
    public void WritesALongTextWithSurrogatePairsUnchanged()
    {
        string text = string.Concat(Enumerable.Repeat("ab\U0001F600\r\n", 20000));
        string path = Path.Combine(_dir, "long.txt");

        FileOps.Real.WriteText(path, text);

        Assert.Equal(new UTF8Encoding(false).GetBytes(text), File.ReadAllBytes(path));
    }

    [Fact]
    public void ADocumentReachesTheDiskBeforeItReplacesTheFile()
    {
        string path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "old");
        var files = new RecordingOps();

        MainWindow.SafeWrite(path, "new", new UTF8Encoding(false), files);

        Assert.Equal(["flush", "replace"], files.Calls);
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public void ANewDocumentReachesTheDiskBeforeItIsMovedInPlace()
    {
        string path = Path.Combine(_dir, "a.txt");
        var files = new RecordingOps();

        MainWindow.SafeWrite(path, "new", new UTF8Encoding(false), files);

        Assert.Equal(["flush", "move"], files.Calls);
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public void TheSessionReachesTheDiskBeforeItReplacesTheOldOne()
    {
        var files = new RecordingOps();
        using var store = new SessionStore(Path.Combine(_dir, "sessions"), files);

        Assert.True(store.Write(new SessionSnapshot { Tabs = { new SessionTab { Text = "keep" } } }, store.NextSequence()));

        Assert.Equal(["flush", "move"], files.Calls);
    }

    [Fact]
    public void TheSettingsReachTheDiskBeforeTheyReplaceTheOldOnes()
    {
        string path = Path.Combine(_dir, "settings.json");
        var files = new RecordingOps();

        new AppSettings { FontSize = 14 }.SaveTo(path, files);

        Assert.Equal(["flush", "move"], files.Calls);
        Assert.Equal(14, AppSettings.LoadFrom(path).FontSize);
    }

    [Fact]
    public void AReplaceThatFailsAfterRemovingTheFileStillLeavesTheNewText()
    {
        string path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "old");

        MainWindow.SafeWrite(path, "new", new UTF8Encoding(false), new HalfReplaceOps(moveFails: false));

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void AReplaceThatFailsAfterRemovingTheFileNeverDeletesTheNewText()
    {
        string path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "old");

        Assert.Throws<IOException>(() =>
            MainWindow.SafeWrite(path, "new", new UTF8Encoding(false), new HalfReplaceOps(moveFails: true)));

        Assert.Equal("new", File.ReadAllText(Assert.Single(Directory.GetFiles(_dir, "*.tmp"))));
    }

    /// <summary>A replace that fails the way ReplaceFile does with ERROR_UNABLE_TO_MOVE_REPLACEMENT
    /// and no backup name: the file is gone and the new text is still under the temp name.</summary>
    private sealed class HalfReplaceOps(bool moveFails) : FileOps
    {
        public override void Replace(string source, string target)
        {
            File.Delete(target);
            throw new IOException("Unable to move the replacement file to the file to be replaced.", unchecked((int)0x80070498));
        }

        public override void Move(string source, string target, bool overwrite)
        {
            if (moveFails)
                throw new IOException("The process cannot access the file because it is being used by another process.");
            base.Move(source, target, overwrite);
        }
    }

    /// <summary>Notes each flush to disk and each replace or move, in order.</summary>
    private sealed class RecordingOps : FileOps
    {
        public List<string> Calls { get; } = new();

        public override FileStream Create(string path) => new RecordingStream(path, Calls);

        public override void Replace(string source, string target)
        {
            Calls.Add("replace");
            base.Replace(source, target);
        }

        public override void Move(string source, string target, bool overwrite)
        {
            Calls.Add("move");
            base.Move(source, target, overwrite);
        }
    }

    private sealed class RecordingStream(string path, List<string> calls)
        : FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)
    {
        public override void Flush(bool flushToDisk)
        {
            if (flushToDisk)
                calls.Add("flush");
            base.Flush(flushToDisk);
        }
    }
}
