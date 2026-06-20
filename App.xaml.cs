using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Jester;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SessionEnding += OnSessionEnding;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // MainWindow restores the previous session and opens any command-line files
        // itself, so both can coexist (e.g. reopened tabs plus an "Open with Jester" file).
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    // Block an OS logoff/shutdown while there are unsaved changes the user wants to keep.
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window && !window.PromptToSaveBeforeShutdown())
            e.Cancel = true;
    }

    // Catch an unexpected UI-thread error, record it, and keep the editor alive rather
    // than letting Windows kill the process and lose the user's other open tabs.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("Dispatcher", e.Exception);
        MessageBox.Show(
            "Jester hit an unexpected error, but your other tabs are still open.\n\n" +
            "Details were saved to:\n" + CrashLogPath + "\n\n" + e.Exception.Message,
            "Jester", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash("AppDomain", ex);
    }

    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jester", "crash.log");

    private static void LogCrash(string source, Exception ex)
    {
        try
        {
            string path = CrashLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:O}] ({source})\n{ex}\n\n");
        }
        catch
        {
            // Never let crash logging itself throw.
        }
    }
}
