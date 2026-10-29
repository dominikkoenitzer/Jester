using System.IO;
using QuestPDF.Drawing.Exceptions;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Exporting to PDF over an existing file. An export that fails must leave the old PDF
/// as it was and no temp file behind.
/// </summary>
public class PdfExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jester_tests_" + Guid.NewGuid().ToString("N"));

    public PdfExportTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ExportWritesAPdf()
    {
        string path = Path.Combine(_dir, "a.pdf");

        PdfExporter.Export(path, "eins\r\nzwei", "a.txt", "Consolas", 11);

        Assert.Equal("%PDF-"u8.ToArray(), File.ReadAllBytes(path)[..5]);
        Assert.Equal([path], Directory.GetFiles(_dir));
    }

    [Fact]
    public void ARenderThatFailsLeavesTheOldPdfAsItWas()
    {
        string path = Path.Combine(_dir, "a.pdf");
        byte[] old = "%PDF-1.7 old"u8.ToArray();
        File.WriteAllBytes(path, old);
        // A title taller than the page cannot be laid out, so the render fails part way.
        string title = string.Concat(Enumerable.Repeat("title ", 20000));

        Assert.Throws<DocumentLayoutException>(() => PdfExporter.Export(path, "text", title, "Consolas", 11));

        Assert.Equal(old, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_dir));
    }

    [Fact]
    public void APdfReachesTheDiskBeforeItReplacesTheOldOne()
    {
        string path = Path.Combine(_dir, "a.pdf");
        byte[] old = "%PDF-1.7 old"u8.ToArray();
        File.WriteAllBytes(path, old);
        var files = new FlushRecordingOps(path);

        PdfExporter.Export(path, "text", "a.txt", "Consolas", 11, files);

        Assert.Equal(old, Assert.Single(files.TargetAtFlush));
        Assert.Equal("%PDF-"u8.ToArray(), File.ReadAllBytes(path)[..5]);
        Assert.Equal([path], Directory.GetFiles(_dir));
    }

    /// <summary>Notes what the target file holds at each flush to disk.</summary>
    private sealed class FlushRecordingOps(string target) : FileOps
    {
        public List<byte[]> TargetAtFlush { get; } = new();

        public override FileStream Create(string path) =>
            new RecordingStream(path, () => TargetAtFlush.Add(File.ReadAllBytes(target)));
    }

    private sealed class RecordingStream(string path, Action flushed)
        : FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)
    {
        public override void Flush(bool flushToDisk)
        {
            base.Flush(flushToDisk);
            if (flushToDisk)
                flushed();
        }
    }
}
