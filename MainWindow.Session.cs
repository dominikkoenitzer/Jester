using System.Windows;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;

namespace Jester;

/// <summary>Keeping every tab, saved or not, across restarts and crashes.</summary>
public partial class MainWindow
{
    // Written this long after the last change, and never later than the longer wait
    // while typing goes on without a pause.
    private static readonly TimeSpan SessionSaveDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SessionSaveMaxWait = TimeSpan.FromSeconds(10);

    private readonly SessionStore _session = new(SessionStore.DefaultDirectory);
    private DispatcherTimer? _sessionTimer;
    private DateTime _sessionPendingSince;
    private bool _sessionClosed;

    /// <summary>Stops writing once the window is gone; the last write was on closing.</summary>
    private void CloseSession()
    {
        _sessionClosed = true;
        _sessionTimer?.Stop();
        _session.Dispose();
    }

    private SessionSnapshot CaptureSession()
    {
        var tabs = _docs.Select(d => SessionState.Capture(
            d.FilePath,
            // A saved file is kept as its path, so its text is not copied out at all. An
            // unsaved one is read once per write, never per keystroke.
            d.FilePath is not null && !d.IsDirty ? "" : d.Editor.Text,
            d.IsDirty,
            d.Editor.CaretOffset,
            d.Encoding,
            d.Disk)).ToList();
        return SessionState.Snapshot(tabs, Tabs.SelectedIndex, DateTime.UtcNow);
    }

    /// <summary>Writes the session now, on this thread. False when it could not be written.</summary>
    private bool SaveSession()
    {
        _sessionTimer?.Stop();
        var snapshot = CaptureSession();
        return _session.Write(snapshot, _session.NextSequence());
    }

    /// <summary>Writes the session soon: a moment after the last change, so typing is
    /// not slowed down, and off the UI thread.</summary>
    private void ScheduleSessionSave()
    {
        if (_sessionClosed)
            return;

        if (_sessionTimer is null)
        {
            _sessionTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = SessionSaveDelay };
            _sessionTimer.Tick += (_, _) => SaveSessionInBackground();
        }

        if (!_sessionTimer.IsEnabled)
            _sessionPendingSince = DateTime.UtcNow;
        else if (DateTime.UtcNow - _sessionPendingSince >= SessionSaveMaxWait)
            return; // Let the pending write happen rather than put it off again.

        _sessionTimer.Stop();
        _sessionTimer.Start();
    }

    private void SaveSessionInBackground()
    {
        _sessionTimer?.Stop();
        // The text is read here, on the UI thread; only the writing moves off it.
        var snapshot = CaptureSession();
        long sequence = _session.NextSequence();
        Task.Run(() => _session.Write(snapshot, sequence));
    }

    /// <summary>Opens the tabs of every stored session that no running window owns, in
    /// their order, unsaved edits included; returns the tab to select.</summary>
    private int RestoreStoredTabs()
    {
        var session = SessionState.Merge(_session.ClaimOrphans());
        var opened = session.Tabs.Select(RestoreTab).ToList();

        var changed = _docs.Where(d => d.ChangedOnDisk).Select(d => d.FilePath!).ToList();
        if (changed.Count > 0)
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => MessageBox.Show(this,
                "These files changed on disk after your last unsaved edits to them:\n\n" +
                string.Join("\n", changed) +
                "\n\nYour edits are open, marked \"changed on disk\". Saving one replaces the version on disk.",
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning));

        return SessionState.RestoredActive(opened, session.ActiveTab);
    }

    private bool RestoreTab(SessionTab stored)
    {
        if (!stored.IsDirty)
        {
            var saved = stored.Path is null ? null : OpenStartupFile(stored.Path);
            if (saved is not null)
                RestoreCaret(saved.Editor, stored.Caret);
            return saved is not null;
        }

        var tab = CreateEmptyTab(select: false);
        LoadInto(tab, stored.Text!, stored.Path, SessionState.ToEncoding(stored.CodePage, stored.Bom));
        // The edits were made against this version of the file, not the one on disk now.
        tab.Disk = stored.Disk;
        tab.IsDirty = true;
        tab.ChangedOnDisk = stored.Path is not null && SessionState.ChangedOnDisk(stored, DiskStamp.Of(stored.Path));
        RestoreCaret(tab.Editor, stored.Caret);
        return true;
    }

    private static void RestoreCaret(TextEditor editor, int caret)
    {
        editor.CaretOffset = Math.Clamp(caret, 0, editor.Document.TextLength);
        if (editor.CaretOffset == 0)
            return;

        // The editor has no layout until its tab is first shown, so scroll it then.
        void ScrollToCaret(object sender, RoutedEventArgs e)
        {
            editor.Loaded -= ScrollToCaret;
            editor.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                var location = editor.Document.GetLocation(editor.CaretOffset);
                editor.ScrollTo(location.Line, location.Column);
            });
        }
        editor.Loaded += ScrollToCaret;
    }
}
