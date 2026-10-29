using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Reading a file must not lose bytes. A file with no byte-order mark is UTF-8
/// only if its bytes are valid UTF-8; anything else is the ANSI code page, and
/// saving with the encoding that was read has to give back the same bytes.
/// </summary>
public class FileEncodingTests : IDisposable
{
    private readonly string _dir;

    public FileEncodingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "jester_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Write(byte[] bytes)
    {
        string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] RoundTrip(string path)
    {
        var (text, encoding) = MainWindow.ReadFile(path);
        string copy = path + ".saved";
        File.WriteAllText(copy, text, encoding);
        return File.ReadAllBytes(copy);
    }

    private static T WithCulture<T>(string name, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // "Grüße" in Windows-1252: ü is 0xFC and ß is 0xDF, neither valid UTF-8.
    private static readonly byte[] AnsiGruesse = { 0x47, 0x72, 0xFC, 0xDF, 0x65 };

    [Fact]
    public void ReadsAnAnsiFileAsTheAnsiCodePage()
    {
        string path = Write(AnsiGruesse);

        var (text, encoding) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        Assert.Equal("Grüße", text);
        Assert.Equal(1252, encoding.CodePage);
    }

    [Fact]
    public void SavesAnAnsiFileBackToTheSameBytes()
    {
        string path = Write(AnsiGruesse);

        Assert.Equal(AnsiGruesse, WithCulture("de-CH", () => RoundTrip(path)));
    }

    [Fact]
    public void TreatsATrailingInvalidByteAsAnsi()
    {
        // A lone lead byte at the very end is still not UTF-8.
        byte[] bytes = { 0x61, 0x62, 0xFC };
        string path = Write(bytes);

        Assert.Equal(bytes, WithCulture("de-CH", () => RoundTrip(path)));
    }

    [Fact]
    public void ReadsValidUtf8WithoutABomAsUtf8()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Grüße");
        string path = Write(bytes);

        var (text, encoding) = MainWindow.ReadFile(path);

        Assert.Equal("Grüße", text);
        Assert.IsType<UTF8Encoding>(encoding);
        Assert.Empty(encoding.GetPreamble());
        Assert.Equal(bytes, RoundTrip(path));
    }

    [Fact]
    public void KeepsAUtf8ByteOrderMark()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Grüße")];
        string path = Write(bytes);

        Assert.Equal(bytes, RoundTrip(path));
    }

    [Fact]
    public void ReadsValidUtf8AfterAByteOrderMarkAsUtf8WithTheMark()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Grüße")];
        string path = Write(bytes);

        var (text, encoding) = MainWindow.ReadFile(path);

        Assert.Equal("Grüße", text);
        Assert.Equal("utf-8-bom", MainWindow.EncodingKey(encoding));
    }

    [Fact]
    public void AStrayByteAfterAUtf8ByteOrderMarkIsKept()
    {
        // The mark claims UTF-8, but 0xE9 between "a" and "b" is not UTF-8, so the file is read as ANSI.
        byte[] bytes = { 0xEF, 0xBB, 0xBF, 0x61, 0xE9, 0x62 };
        string path = Write(bytes);

        var (text, _) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        Assert.DoesNotContain('\uFFFD', text);
        Assert.Equal("ï»¿aéb", text);
        Assert.Equal(bytes, WithCulture("de-CH", () => RoundTrip(path)));
    }

    [Fact]
    public void KeepsUtf16LittleEndian()
    {
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("Grüße")];
        string path = Write(bytes);

        var (text, _) = MainWindow.ReadFile(path);

        Assert.Equal("Grüße", text);
        Assert.Equal(bytes, RoundTrip(path));
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    public void DecodesInTheChosenEncodingWithoutItsByteOrderMark(string kind)
    {
        var encoding = MainWindow.ReopenEncoding(kind);
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes("Grüße")];

        Assert.Equal("Grüße", MainWindow.Decode(bytes, encoding));
    }

    [Fact]
    public void DropsAUtf8ByteOrderMarkWhenReopenedAsUtf8WithoutOne()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Grüße")];

        Assert.Equal("Grüße", MainWindow.Decode(bytes, MainWindow.ReopenEncoding("utf-8")));
    }

    [Fact]
    public void DecodesWindows1252()
    {
        byte[] bytes = { 0x4B, 0xE4, 0x73, 0x65 };

        Assert.Equal("Käse", MainWindow.Decode(bytes, MainWindow.ReopenEncoding("windows-1252")));
    }

    [Fact]
    public void ReopensAUtf8FileAsWindows1252AndSavesTheSameBytes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Grüße");
        string path = Write(bytes);

        var (text, encoding) = MainWindow.ReadFile(path, MainWindow.ReopenEncoding("windows-1252"));
        string copy = path + ".saved";
        File.WriteAllText(copy, text, encoding);

        Assert.Equal("GrÃ¼ÃŸe", text);
        Assert.Equal(1252, encoding.CodePage);
        Assert.Equal(bytes, File.ReadAllBytes(copy));
    }

    [Theory]
    [InlineData("windows-1252")]
    [InlineData("utf-16le")]
    public void AReopenedFileComesBackInTheSameEncodingAfterARestart(string kind)
    {
        string path = Write(Encoding.UTF8.GetBytes("Grüß"));
        var (text, encoding) = MainWindow.ReadFile(path, MainWindow.ReopenEncoding(kind));
        var stored = SessionState.Capture(path, "", isDirty: false, 0, encoding, DiskStamp.Of(path))!;

        var kept = SessionState.KeptEncoding(stored, DiskStamp.Of(path));
        var (restored, restoredEncoding) = kept is null ? MainWindow.ReadFile(path) : MainWindow.ReadFile(path, kept);

        Assert.NotEqual("Grüß", text);
        Assert.Equal(text, restored);
        Assert.Equal(encoding.CodePage, restoredEncoding.CodePage);
    }

    [Theory]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    public void ReopenedFilesSaveBackWithTheirByteOrderMark(string kind)
    {
        var encoding = MainWindow.ReopenEncoding(kind);
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes("Grüße")];
        string path = Write(bytes);

        var (text, read) = MainWindow.ReadFile(path, encoding);
        string copy = path + ".saved";
        File.WriteAllText(copy, text, read);

        Assert.Equal("Grüße", text);
        Assert.Equal(bytes, File.ReadAllBytes(copy));
    }

    [Fact]
    public void AnsiSaveKeepsEveryCharacterOrRefuses()
    {
        string path = Write(AnsiGruesse);
        string text = "Grüsse → ✓ \U0001F600";
        var ansi = MainWindow.ReopenEncoding("windows-1252");

        try
        {
            MainWindow.SafeWrite(path, text, ansi, FileOps.Real);
        }
        catch (EncoderFallbackException)
        {
            Assert.Equal(AnsiGruesse, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(_dir));
            return;
        }

        Assert.Equal(text, MainWindow.ReadFile(path, ansi).Text);
    }

    [Fact]
    public void AnsiSaveRefusesABestFitReplacement()
    {
        string path = Path.Combine(_dir, "a.txt");

        // U+0100 has no slot in Windows-1252; best fit would write a plain "A".
        Assert.Throws<EncoderFallbackException>(
            () => MainWindow.SafeWrite(path, "Ā", MainWindow.ReopenEncoding("windows-1252"), FileOps.Real));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void AnsiSaveReplacesOnlyWhenAllowed()
    {
        string path = Path.Combine(_dir, "a.txt");

        MainWindow.SafeWrite(path, "a→b", MainWindow.ReopenEncoding("windows-1252"), FileOps.Real, replaceUnmappable: true);

        Assert.Equal(new byte[] { 0x61, 0x3F, 0x62 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void SavingATabReplacesCharactersOnlyAfterTheUserAllowedIt()
    {
        string path = Write(AnsiGruesse);

        StaThread.Run(() =>
        {
            var tab = new DocumentTab { FilePath = path, Encoding = MainWindow.ReopenEncoding("windows-1252") };
            tab.Editor.Text = "a→b";
            tab.IsDirty = true;

            Assert.Throws<EncoderFallbackException>(() => MainWindow.WriteTab([tab], tab, path, FileOps.Real));
            Assert.True(tab.IsDirty);
            Assert.Equal(AnsiGruesse, File.ReadAllBytes(path));

            MainWindow.WriteTab([tab], tab, path, FileOps.Real, replaceUnmappable: true);
            Assert.False(tab.IsDirty);
            Assert.Equal(new byte[] { 0x61, 0x3F, 0x62 }, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void SafeWriteSavesAnAnsiFileBackToTheSameBytes()
    {
        string path = Write(AnsiGruesse);
        var (text, encoding) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        MainWindow.SafeWrite(path, text, encoding, FileOps.Real);

        Assert.Equal(AnsiGruesse, File.ReadAllBytes(path));
    }

    [Fact]
    public void FindInFilesFindsAnUmlautInAnAnsiFile()
    {
        File.WriteAllBytes(Path.Combine(_dir, "b.txt"), new byte[] { 0x61, 0x0D, 0x0A, 0x47, 0x72, 0xFC, 0x73, 0x73, 0x65 });

        var results = WithCulture("de-CH", () => MainWindow.SearchFolder(_dir, "Grüsse", new[] { "*" }, false, false));

        var result = Assert.Single(results);
        Assert.Equal(2, result.Line);
        Assert.Equal(1, result.Column);
        Assert.Equal("Grüsse", result.Preview);
    }

    // At most 500 characters of the line, plus an ellipsis at each end that was cut.
    private const int LongestPreview = 502;

    [Fact]
    public void FindInFilesKeepsAShortPreviewAroundTheMatchOnAVeryLongLine()
    {
        string line = new string('x', 600_000) + "needle" + new string('y', 399_994);
        File.WriteAllText(Path.Combine(_dir, "min.js"), line);

        var result = Assert.Single(MainWindow.SearchFolder(_dir, "needle", new[] { "*" }, false, false));

        Assert.Equal(600_001, result.Column);
        Assert.InRange(result.Preview.Length, 1, LongestPreview);
        Assert.Matches("^…x+needley+…$", result.Preview);
    }

    [Fact]
    public void FindInFilesStartsTheLongPreviewAtTheLineWhenTheMatchIsNearIt()
    {
        File.WriteAllText(Path.Combine(_dir, "start.js"), "  needle" + new string('y', 1_000_000));
        File.WriteAllText(Path.Combine(_dir, "end.js"), new string('x', 1_000_000) + "needle  ");

        var results = MainWindow.SearchFolder(_dir, "needle", new[] { "*" }, false, false);

        Assert.Equal(2, results.Count);
        var start = results.Single(r => r.FilePath.EndsWith("start.js", StringComparison.Ordinal));
        var end = results.Single(r => r.FilePath.EndsWith("end.js", StringComparison.Ordinal));
        Assert.InRange(start.Preview.Length, 1, LongestPreview);
        Assert.Matches("^needley+…$", start.Preview);
        Assert.InRange(end.Preview.Length, 1, LongestPreview);
        Assert.Matches("^…x+needle$", end.Preview);
    }

    [Fact]
    public void FindInFilesNeverCutsACharacterInHalfForThePreview()
    {
        string pairs = string.Concat(Enumerable.Repeat("😀", 50_000));
        // One more character before or after the match moves each cut by one, onto both halves of a pair.
        File.WriteAllLines(Path.Combine(_dir, "emoji.txt"), new[]
        {
            pairs + "needle" + pairs,
            pairs + "aneedle" + pairs,
            pairs + "needleb" + pairs,
            pairs + "aneedleb" + pairs,
        });

        var results = MainWindow.SearchFolder(_dir, "needle", new[] { "*" }, false, false);

        Assert.Equal(4, results.Count);
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        foreach (var result in results)
        {
            Assert.InRange(result.Preview.Length, 1, LongestPreview);
            Assert.Contains("needle", result.Preview);
            strict.GetBytes(result.Preview);
        }
    }

    [Fact]
    public void FindInFilesKeepsTheWholeTrimmedTextOfANormalLine()
    {
        string full = new string('z', 494) + "needle";
        File.WriteAllLines(Path.Combine(_dir, "code.cs"), new[] { "\t  var a = needle(); // note  ", full });

        var results = MainWindow.SearchFolder(_dir, "needle", new[] { "*" }, false, false);

        Assert.Equal(new[] { "var a = needle(); // note", full }, results.Select(r => r.Preview));
    }

    [Fact]
    public void DoesNotTickUtf8InTheMenuForAnAnsiFile()
    {
        string path = Write(AnsiGruesse);
        var (_, encoding) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        Assert.NotEqual("utf-8", MainWindow.EncodingKey(encoding));
    }
}
