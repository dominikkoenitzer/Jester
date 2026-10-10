using System.Globalization;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// F5 writes the short time and then the short date, in the formats of the user's
/// culture rather than always the US ones.
/// </summary>
public class DateTimeStampTests
{
    private static readonly DateTime Afternoon = new(2026, 3, 7, 14, 5, 0);

    // Without user overrides, so the result does not depend on this PC's regional settings.
    private static CultureInfo Culture(string name) => new(name, useUserOverride: false);

    [Fact]
    public void UsesTheUsFormatsForEnUs()
    {
        string stamp = MainWindow.DateTimeStamp(Afternoon, Culture("en-US"));

        // Newer ICU data puts a narrow no-break space before AM/PM.
        Assert.Equal("2:05 PM 3/7/2026", stamp.Replace(' ', ' '));
    }

    [Fact]
    public void UsesTheSwissFormatsForDeCh()
    {
        Assert.Equal("14:05 07.03.2026", MainWindow.DateTimeStamp(Afternoon, Culture("de-CH")));
    }

    [Fact]
    public void PutsTheShortTimeBeforeTheShortDate()
    {
        var culture = Culture("fr-FR");
        string expected = Afternoon.ToString("t", culture) + " " + Afternoon.ToString("d", culture);

        Assert.Equal(expected, MainWindow.DateTimeStamp(Afternoon, culture));
    }
}
