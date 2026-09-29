using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Jester;

/// <summary>
/// Model for one open document/tab: its editor view plus the per-file state (path,
/// encoding, dirty flag). The tab header binds to <see cref="Header"/> and
/// <see cref="ToolTip"/>, which update automatically as the file is saved or edited.
/// </summary>
internal sealed class DocumentTab : INotifyPropertyChanged
{
    private static int _untitledCounter;

    private string? _filePath;
    private bool _isDirty;
    private bool _changedOnDisk;

    public EditorView View { get; } = new();

    public TextBox Editor => View.Editor;

    public string UntitledName { get; }

    public DocumentTab()
    {
        int n = ++_untitledCounter;
        UntitledName = n == 1 ? "Untitled" : $"Untitled {n}";
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            Notify(nameof(Header));
            Notify(nameof(ToolTip));
            Notify(nameof(Name));
        }
    }

    public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The file on disk as it was last read or saved, to tell whether it changed since.</summary>
    public DiskStamp? Disk { get; set; }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
                return;
            _isDirty = value;
            Notify(nameof(Header));
        }
    }

    /// <summary>
    /// Unsaved edits restored from the last session while the file on disk has changed
    /// since they began. Saving replaces that newer version, so the tab says so until then.
    /// </summary>
    public bool ChangedOnDisk
    {
        get => _changedOnDisk;
        set
        {
            if (_changedOnDisk == value)
                return;
            _changedOnDisk = value;
            Notify(nameof(Header));
            Notify(nameof(ToolTip));
        }
    }

    /// <summary>File name (or the assigned "Untitled" name) with no dirty marker.</summary>
    public string Name => FilePath is null ? UntitledName : Path.GetFileName(FilePath);

    /// <summary>Tab caption: the name, prefixed with "*" while there are unsaved edits.</summary>
    public string Header => (IsDirty ? "*" : "") + Name + (ChangedOnDisk ? " (changed on disk)" : "");

    /// <summary>Hover tooltip: the full path, or the placeholder name for new buffers.</summary>
    public string ToolTip => (FilePath ?? UntitledName) +
        (ChangedOnDisk ? "\nChanged on disk since these unsaved edits. Saving replaces that version." : "");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
