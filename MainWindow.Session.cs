using System.Windows.Threading;

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

    private SessionSnapshot CaptureSession()
    {
        var tabs = _docs.Select(d => SessionState.Capture(
            d.FilePath,
            // A saved file is kept as its path, so its text is not copied out at all.
            d.FilePath is not null && !d.IsDirty ? "" : d.Editor.Text,
            d.IsDirty,
            d.Editor.CaretIndex,
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
}
