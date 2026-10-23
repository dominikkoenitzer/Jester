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
            MainWindow.SafeWrite(path, text, ansi);
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
            () => MainWindow.SafeWrite(path, "Ā", MainWindow.ReopenEncoding("windows-1252")));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void AnsiSaveReplacesOnlyWhenAllowed()
    {
        string path = Path.Combine(_dir, "a.txt");

        MainWindow.SafeWrite(path, "a→b", MainWindow.ReopenEncoding("windows-1252"), replaceUnmappable: true);

        Assert.Equal(new byte[] { 0x61, 0x3F, 0x62 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void SafeWriteSavesAnAnsiFileBackToTheSameBytes()
    {
        string path = Write(AnsiGruesse);
        var (text, encoding) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        MainWindow.SafeWrite(path, text, encoding);

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

    [Fact]
    public void DoesNotTickUtf8InTheMenuForAnAnsiFile()
    {
        string path = Write(AnsiGruesse);
        var (_, encoding) = WithCulture("de-CH", () => MainWindow.ReadFile(path));

        Assert.NotEqual("utf-8", MainWindow.EncodingKey(encoding));
    }
}
