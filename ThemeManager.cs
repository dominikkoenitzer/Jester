using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Jester;

/// <summary>
/// Follows the Windows app mode: the dark palette when apps are set to dark, the light
/// one otherwise, swapped live when the setting changes. The palette is the first merged
/// dictionary of the application's resources; every style reads it as a DynamicResource.
/// </summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark { get; private set; }

    /// <summary>Applies the Windows app mode now and again whenever it changes.</summary>
    public static void FollowSystem()
    {
        Apply(SystemUsesDarkApps());
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.General)
                return;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                bool dark = SystemUsesDarkApps();
                if (dark != IsDark)
                    Apply(dark);
            });
        };
    }

    /// <summary>Whether Windows has apps in dark mode. A missing value means light, as in Windows.</summary>
    internal static bool SystemUsesDarkApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(bool dark)
    {
        if (Application.Current is not { } app)
            return;

        var palette = new ResourceDictionary
        {
            Source = new Uri($"/Jester;component/Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
        var dictionaries = app.Resources.MergedDictionaries;
        if (dictionaries.Count > 0 && dictionaries[0].Source?.OriginalString.Contains("Themes/") == true)
            dictionaries[0] = palette;
        else
            dictionaries.Insert(0, palette);

        IsDark = dark;
        SyntaxPalette.NotifyChanged();
        foreach (Window window in app.Windows)
            ApplyFrame(window);
    }

    /// <summary>Tells Windows which mode the window is in, so its frame, the system menu and
    /// the corner rounding match the content.</summary>
    public static void ApplyFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        int dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    // Older Windows versions do not know these attributes and return an error, which is fine.
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
