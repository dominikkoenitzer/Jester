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

    /// <summary>File name (or the assigned "Untitled" name) with no dirty marker.</summary>
    public string Name => FilePath is null ? UntitledName : Path.GetFileName(FilePath);

    /// <summary>Tab caption: the name, prefixed with "*" while there are unsaved edits.</summary>
    public string Header => (IsDirty ? "*" : "") + Name;

    /// <summary>Hover tooltip: the full path, or the placeholder name for new buffers.</summary>
    public string ToolTip => FilePath ?? UntitledName;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
