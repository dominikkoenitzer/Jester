using Xunit;

namespace Jester.Tests;

/// <summary>What crash.log keeps of a crash.</summary>
public class CrashLogTests
{
    [Fact]
    public void ACrashIsLoggedWithoutAClockTime()
    {
        string entry = App.CrashEntry("Dispatcher", new InvalidOperationException("it broke"));

        Assert.Contains("(Dispatcher)", entry);
        Assert.Contains("it broke", entry);
        Assert.DoesNotMatch(@"\d\d:\d\d", entry);
    }
}
