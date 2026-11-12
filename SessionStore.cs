using System.IO;
using System.Text.Json;

namespace Jester;

/// <summary>
/// Where the session lives on disk: one file per running window under
/// <c>%AppData%\Jester\sessions</c>, beside settings.json, so two windows never write
/// over each other. Each window holds a lock file for as long as it runs. On start a
/// window takes over every session whose lock is free (a window that was closed or
/// crashed), and deletes those files once its own session, which now holds their tabs,
/// is safely written. A file named order holds the count that puts the sessions of all
/// windows in the order they were written.
/// </summary>
internal sealed class SessionStore : IDisposable
{
    private readonly string _directory;
    private readonly string _path;
    private readonly FileOps _files;
    private readonly FileStream? _lock;
    private readonly object _writeGate = new();
    private readonly List<(string Path, FileStream Claim)> _adopted = new();
    private long _sequence;
    private long _written;
    private long _order;

    private const string OrderFileName = "order";

    public static string DefaultDirectory => System.IO.Path.Combine(AppPaths.DataDirectory, "sessions");

    public SessionStore(string directory) : this(directory, FileOps.Real)
    {
    }

    internal SessionStore(string directory, FileOps files)
    {
        _directory = directory;
        _files = files;
        HadSessions = Directory.Exists(directory);

        string id = Guid.NewGuid().ToString("N");
        _path = System.IO.Path.Combine(directory, id + ".json");
        try
        {
            Directory.CreateDirectory(directory);
            _lock = TryLock(id);
        }
        catch
        {
            // Without the folder nothing can be kept; Write reports that by returning false.
        }
    }

    /// <summary>Whether a session was ever stored here. Before that, the file list in
    /// settings.json is the only session there is.</summary>
    public bool HadSessions { get; }

    /// <summary>This window's session file.</summary>
    public string Path => _path;

    /// <summary>
    /// Takes over the sessions of windows that are no longer running. Their files stay on
    /// disk until this window's session is written, so a crash in between loses nothing.
    /// A session that cannot be read is left where it is rather than deleted.
    /// </summary>
    public List<SessionSnapshot> ClaimOrphans()
    {
        var snapshots = new List<SessionSnapshot>();
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(_directory, "*.json").ToList(); }
        catch { return snapshots; }

        foreach (string file in files)
        {
            if (string.Equals(file, _path, StringComparison.OrdinalIgnoreCase))
                continue;

            var claim = TryLock(System.IO.Path.GetFileNameWithoutExtension(file));
            if (claim is null)
                continue; // That window is still running.

            try
            {
                var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(file));
                if (snapshot is not null)
                {
                    _order = Math.Max(_order, snapshot.Order);
                    snapshots.Add(snapshot);
                    _adopted.Add((file, claim));
                    continue;
                }
            }
            catch
            {
                // Gone already, or unreadable: leave it alone.
            }
            claim.Dispose();
        }
        return snapshots;
    }

    /// <summary>A number for a snapshot taken now. A write only replaces the file with a
    /// later snapshot, whichever order the writes finish in.</summary>
    public long NextSequence() => Interlocked.Increment(ref _sequence);

    /// <summary>
    /// Writes the snapshot through a temp file that then replaces the session, so a crash
    /// mid-write leaves the previous session whole. Safe to call from any thread. Returns
    /// false when it could not be written.
    /// </summary>
    public bool Write(SessionSnapshot snapshot, long sequence)
    {
        lock (_writeGate)
        {
            if (sequence <= _written)
                return true;

            string temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                snapshot.Order = NextOrder();
                _files.WriteText(temp, JsonSerializer.Serialize(snapshot));
                _files.Move(temp, _path, overwrite: true);
                _written = sequence;
            }
            catch
            {
                try { File.Delete(temp); }
                catch { /* best-effort cleanup of the temp file */ }
                return false;
            }

            // The adopted tabs are in this session now. A file that cannot be deleted yet stays
            // claimed, so no other window takes its tabs as well, and is tried again next time.
            DeleteAdopted();
            return true;
        }
    }

    /// <summary>
    /// The next number of the count all windows share. The file is held alone while it is
    /// read and written; when it cannot be had in time, the count goes on from this
    /// window's last number.
    /// </summary>
    private long NextOrder()
    {
        string path = System.IO.Path.Combine(_directory, OrderFileName);
        for (int attempt = 0; attempt < 7; attempt++)
        {
            try
            {
                using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                using var reader = new StreamReader(file, leaveOpen: true);
                long.TryParse(reader.ReadToEnd().Trim(), out long stored);
                _order = Math.Max(stored, _order) + 1;
                // Always the same width, so the new number covers the old one whole.
                byte[] text = System.Text.Encoding.ASCII.GetBytes(_order.ToString("D19", System.Globalization.CultureInfo.InvariantCulture));
                file.Position = 0;
                file.Write(text, 0, text.Length);
                return _order;
            }
            catch (IOException)
            {
                Thread.Sleep(7); // Another window has it.
            }
            catch
            {
                break;
            }
        }
        return ++_order;
    }

    public void Dispose()
    {
        lock (_writeGate)
        {
            if (_written > 0)
                DeleteAdopted();
            foreach (var (_, claim) in _adopted)
                claim.Dispose();
            _adopted.Clear();
            _lock?.Dispose();
        }
    }

    private void DeleteAdopted() => _adopted.RemoveAll(adopted =>
    {
        try { File.Delete(adopted.Path); }
        catch { return false; }
        adopted.Claim.Dispose();
        return true;
    });

    // Held open with no sharing for as long as a window runs. Windows deletes it when the
    // handle closes, even when the process is killed.
    private FileStream? TryLock(string id)
    {
        try
        {
            return new FileStream(System.IO.Path.Combine(_directory, id + ".lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
        }
        catch
        {
            return null;
        }
    }
}
