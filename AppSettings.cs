using System.IO;
using System.Text.Json;

namespace Jester;

/// <summary>
/// User preferences and last session, persisted as JSON under
/// <c>%AppData%\Jester\settings.json</c>. Loading and saving never throw — a missing
/// or corrupt file simply falls back to defaults so the app always starts.
/// </summary>
internal sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 950;
    public double WindowHeight { get; set; } = 640;
    public bool WindowMaximized { get; set; }

    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 11;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public double Zoom { get; set; } = 1.0;

    public bool WordWrap { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    public bool AutoIndent { get; set; } = true;
    public bool StatusBarVisible { get; set; } = true;

    public List<string> RecentFiles { get; set; } = new();
    public List<string> OpenFiles { get; set; } = new();
    public int ActiveTab { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Jester", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            string path = SettingsPath;
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable settings: start clean rather than failing to launch.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort: never let a failed settings write crash a close/shutdown.
        }
    }
}
