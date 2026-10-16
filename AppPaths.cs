using System.IO;

namespace Jester;

/// <summary>Where Jester keeps what it remembers: <c>%AppData%\Jester</c>. Settable so a
/// test or a second copy can run against a folder of its own.</summary>
internal static class AppPaths
{
    public static string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jester");
}
