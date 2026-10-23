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
}
