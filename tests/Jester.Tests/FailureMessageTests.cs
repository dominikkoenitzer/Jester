using System.IO;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// A file that cannot be opened, saved or exported is reported in one plain sentence
/// that names the file. The exception's own text never reaches the user.
/// </summary>
public class FailureMessageTests : IDisposable
{
    private const string Detail = "Access to the path 'C:\\x\\y' is denied. 0x80070005";

    private readonly string _dir;

    public FailureMessageTests()
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

    private static Exception ReadFailure(string path) =>
        Assert.ThrowsAny<Exception>(() => MainWindow.ReadFile(path));

    private static IOException IOFailure(int code) => new(Detail, unchecked((int)0x80070000) | code);

    [Fact]
    public void ADroppedFolderIsCalledAFolder()
    {
        string folder = Path.Combine(_dir, "Notes");
        Directory.CreateDirectory(folder);

        Assert.Equal("Notes is a folder, not a file.", MainWindow.OpenFailure(folder, ReadFailure(folder)));
    }

    [Fact]
    public void AMissingFileIsCalledMissing()
    {
        string path = Path.Combine(_dir, "gone.txt");

        Assert.Equal("Could not find gone.txt.", MainWindow.OpenFailure(path, ReadFailure(path)));
    }

    [Fact]
    public void AFileHeldOpenElsewhereIsCalledInUse()
    {
        string path = Path.Combine(_dir, "held.txt");
        File.WriteAllText(path, "x");
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal("held.txt is in use by another program.", MainWindow.OpenFailure(path, ReadFailure(path)));
    }

    [Fact]
    public void AFileWithoutReadAccessSaysSo()
    {
        string path = Path.Combine(_dir, "locked.txt");

        Assert.Equal("You do not have permission to open locked.txt.",
            MainWindow.OpenFailure(path, new UnauthorizedAccessException(Detail)));
    }

    [Fact]
    public void ASaveIntoAFolderThatIsGoneSaysSo()
    {
        string path = Path.Combine(_dir, "gone", "a.txt");
        var ex = Assert.ThrowsAny<Exception>(() => File.WriteAllText(path, "x"));

        Assert.Equal("The folder for a.txt no longer exists.", MainWindow.SaveFailure(path, ex));
    }

    [Fact]
    public void EachSaveFailureGetsItsOwnSentence()
    {
        string path = Path.Combine(_dir, "a.txt");

        Assert.Equal("You do not have permission to save a.txt there.",
            MainWindow.SaveFailure(path, new UnauthorizedAccessException(Detail)));
        Assert.Equal("There is not enough disk space to save a.txt.", MainWindow.SaveFailure(path, IOFailure(112)));
        Assert.Equal("a.txt is in use by another program.", MainWindow.SaveFailure(path, IOFailure(32)));
        Assert.Equal("Could not save a.txt.", MainWindow.SaveFailure(path, new InvalidOperationException(Detail)));
    }

    [Fact]
    public void AnExportFailureNamesThePdf()
    {
        string path = Path.Combine(_dir, "a.pdf");

        Assert.Equal("There is not enough disk space to save a.pdf.", MainWindow.ExportFailure(path, IOFailure(39)));
        Assert.Equal("Could not export a.pdf.", MainWindow.ExportFailure(path, new InvalidOperationException(Detail)));
    }

    public static TheoryData<Exception> Failures() => new()
    {
        new IOException(Detail),
        IOFailure(33),
        new UnauthorizedAccessException(Detail),
        new FileNotFoundException(Detail),
        new DirectoryNotFoundException(Detail),
        new PathTooLongException(Detail),
        new InvalidOperationException(Detail),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void NoMessageCarriesTheExceptionText(Exception ex)
    {
        string path = Path.Combine(_dir, "a.txt");
        foreach (string message in new[]
                 {
                     MainWindow.OpenFailure(path, ex),
                     MainWindow.SaveFailure(path, ex),
                     MainWindow.ExportFailure(path, ex),
                 })
        {
            Assert.DoesNotContain("denied", message);
            Assert.DoesNotContain("0x", message);
            Assert.DoesNotContain('\n', message);
            Assert.EndsWith(".", message);
        }
    }
}
