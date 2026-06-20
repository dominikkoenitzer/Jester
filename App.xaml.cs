using System.Windows;

namespace Jester;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SessionEnding += OnSessionEnding;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // Open a file passed on the command line (e.g. "Open with Jester"). OpenFile
        // surfaces its own message if the path is missing or unreadable, instead of
        // silently starting with a blank document.
        if (e.Args.Length > 0)
            window.OpenFile(e.Args[0]);
    }

    // Block an OS logoff/shutdown while there are unsaved changes the user wants to keep.
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window && !window.PromptToSaveBeforeShutdown())
            e.Cancel = true;
    }
}
