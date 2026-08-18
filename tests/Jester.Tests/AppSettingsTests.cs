using System.IO;
using System.Text.Json;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// Settings persistence. The contract that matters is the one in the class's own
/// summary: loading and saving never throw, so a missing or corrupt file can
/// never stop Jester from starting.
/// </summary>
public class AppSettingsTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public AppSettingsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "jester_tests_" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AFreshInstallGetsTheDocumentedDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(950, settings.WindowWidth);
        Assert.Equal(640, settings.WindowHeight);
        Assert.Equal("Consolas", settings.FontFamily);
        Assert.Equal(11, settings.FontSize);
        Assert.Equal(1.0, settings.Zoom);
        Assert.True(settings.ShowLineNumbers);
        Assert.True(settings.AutoIndent);
        Assert.True(settings.StatusBarVisible);
        Assert.False(settings.WordWrap);
        Assert.False(settings.Bold);
        Assert.Empty(settings.RecentFiles);
        Assert.Empty(settings.OpenFiles);
    }

    [Fact]
    public void SettingsSurviveASaveAndLoadRoundTrip()
    {
        var saved = new AppSettings
        {
            WindowLeft = 120,
            WindowTop = 80,
            WindowWidth = 1280,
            WindowHeight = 800,
            WindowMaximized = true,
            FontFamily = "Cascadia Code",
            FontSize = 13.5,
            Bold = true,
            Italic = true,
            Zoom = 1.25,
            WordWrap = true,
            ShowLineNumbers = false,
            AutoIndent = false,
            StatusBarVisible = false,
            ActiveTab = 2,
            RecentFiles = { @"C:\a.txt", @"C:\b.txt" },
            OpenFiles = { @"C:\a.txt" },
        };

        saved.SaveTo(_path);
        var loaded = AppSettings.LoadFrom(_path);

        Assert.Equal(120, loaded.WindowLeft);
        Assert.Equal(80, loaded.WindowTop);
        Assert.Equal(1280, loaded.WindowWidth);
        Assert.Equal(800, loaded.WindowHeight);
        Assert.True(loaded.WindowMaximized);
        Assert.Equal("Cascadia Code", loaded.FontFamily);
        Assert.Equal(13.5, loaded.FontSize);
        Assert.True(loaded.Bold);
        Assert.True(loaded.Italic);
        Assert.Equal(1.25, loaded.Zoom);
        Assert.True(loaded.WordWrap);
        Assert.False(loaded.ShowLineNumbers);
        Assert.False(loaded.AutoIndent);
        Assert.False(loaded.StatusBarVisible);
        Assert.Equal(2, loaded.ActiveTab);
        Assert.Equal(new[] { @"C:\a.txt", @"C:\b.txt" }, loaded.RecentFiles);
        Assert.Equal(new[] { @"C:\a.txt" }, loaded.OpenFiles);
    }

    [Fact]
    public void SavingCreatesTheDirectoryItNeeds()
    {
        Assert.False(Directory.Exists(_dir));

        new AppSettings().SaveTo(_path);

        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void AMissingFileFallsBackToDefaults()
    {
        var loaded = AppSettings.LoadFrom(Path.Combine(_dir, "not_written_yet.json"));
        Assert.Equal(950, loaded.WindowWidth);
        Assert.Equal("Consolas", loaded.FontFamily);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"WindowWidth\": \"not a number\"}")]
    public void ACorruptFileFallsBackToDefaultsInsteadOfThrowing(string contents)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, contents);

        var loaded = AppSettings.LoadFrom(_path);

        Assert.Equal(950, loaded.WindowWidth);
        Assert.Equal("Consolas", loaded.FontFamily);
    }

    [Fact]
    public void UnknownKeysFromANewerBuildAreIgnored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """
            { "WindowWidth": 1024, "SomeFutureSetting": true }
            """);

        var loaded = AppSettings.LoadFrom(_path);

        Assert.Equal(1024, loaded.WindowWidth);
        Assert.Equal(640, loaded.WindowHeight);  // absent, so the default stands
    }

    [Fact]
    public void SavingToAnUnwritablePathDoesNotThrow()
    {
        // Shutdown calls Save(). A locked or read-only profile must not turn
        // closing the app into a crash.
        var settings = new AppSettings();
        var exception = Record.Exception(() => settings.SaveTo(@"Z:\no_such_drive\settings.json"));
        Assert.Null(exception);
    }

    [Fact]
    public void TheWrittenFileIsReadableJson()
    {
        new AppSettings { FontFamily = "Consolas" }.SaveTo(_path);

        using var document = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal("Consolas", document.RootElement.GetProperty("FontFamily").GetString());
    }
}
