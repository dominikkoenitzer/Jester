using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace Jester;

/// <summary>
/// Reads a stretch of a <see cref="TextBox"/>'s text without building the whole string.
/// The first read of <see cref="TextBox.Text"/> after an edit copies the entire document
/// (some 40 ms at 20 MB), so reading it on every keystroke made typing in a large file
/// slow however little else happened. The TextBox keeps its text in a container of
/// <see cref="TextPointer"/> positions that WPF exposes on a RichTextBox but not on a
/// TextBox; this reaches it by name. Should a WPF release rename it, every read falls
/// back to <see cref="TextBox.Text"/>, which is slower but gives the same answer.
/// </summary>
internal sealed class TextBoxContent
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static readonly PropertyInfo? ContainerProperty =
        typeof(TextBoxBase).GetProperty("TextContainer", AnyInstance);

    private readonly TextBox _box;
    private readonly object? _container;
    private readonly PropertyInfo? _startProperty;
    private readonly PropertyInfo? _endProperty;

    public TextBoxContent(TextBox box)
    {
        _box = box;
        _container = ContainerProperty?.GetValue(box);
        var type = _container?.GetType();
        _startProperty = type?.GetProperty("Start", AnyInstance);
        _endProperty = type?.GetProperty("End", AnyInstance);
    }

    private TextPointer? Start => _startProperty?.GetValue(_container) as TextPointer;

    private TextPointer? End => _endProperty?.GetValue(_container) as TextPointer;

    /// <summary>Whether reads go straight to the TextBox's text rather than through its Text property.</summary>
    public bool IsDirect => Start is not null && End is not null;

    /// <summary>The length of the text.</summary>
    public int Length => Start is { } start && End is { } end ? start.GetOffsetToPosition(end) : _box.Text.Length;

    /// <summary>The <paramref name="length"/> characters of the text from <paramref name="start"/>.</summary>
    public string Read(int start, int length)
    {
        if (length == 0)
            return "";

        var buffer = new char[length];
        int filled = 0;
        var position = Start?.GetPositionAtOffset(start, LogicalDirection.Forward);
        while (position is not null && filled < length)
        {
            int read = position.GetTextInRun(LogicalDirection.Forward, buffer, filled, length - filled);
            if (read == 0)
                break; // Not text; a TextBox holds nothing else, so trust Text instead.
            filled += read;
            position = position.GetPositionAtOffset(read, LogicalDirection.Forward);
        }

        return filled == length ? new string(buffer) : _box.Text.Substring(start, length);
    }
}
