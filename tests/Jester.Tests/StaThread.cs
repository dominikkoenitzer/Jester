using System.Runtime.ExceptionServices;

namespace Jester.Tests;

/// <summary>Runs a test on an STA thread, where WPF controls can live without a window.</summary>
internal static class StaThread
{
    public static void Run(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
