using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Jester;

/// <summary>Text-entry behaviour: auto-indent, ctrl-wheel zoom, drag and drop.</summary>
public partial class MainWindow
{
    // ------------------------------------------------------------- Edit extras

    private void InsertDateTime()
    {
        if (ActiveEditor is not { } ed)
            return;

        string stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
        int caret = ed.SelectionStart;
        ed.SelectedText = stamp;
        ed.CaretIndex = caret + stamp.Length;
        ed.Focus();
    }

    private void Editor_PreviewKeyDown(DocumentTab tab, KeyEventArgs e)
    {
        // Enter is handled even without auto-indent: the TextBox would insert CRLF whatever
        // the file uses. Shift+Enter is a line break too, but has never auto-indented.
        if (e.Key != Key.Return)
            return;
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
            return;

        var ed = tab.Editor;
        var lines = tab.View.Lines;
        int start = ed.SelectionStart;
        bool indent = _autoIndent && modifiers == ModifierKeys.None;
        // Only the current line up to the caret decides the indent.
        int lineStart = lines.StartOf(lines.LineAt(start));
        string before = indent ? tab.View.Read(lineStart, start - lineStart) : "";
        string lineBreak = LineBreakAt(lines.LineEnding, before, before.Length, indent);

        ed.SelectedText = lineBreak;
        ed.CaretIndex = start + lineBreak.Length;
        ed.SelectionLength = 0;
        e.Handled = true;
    }

    /// <summary>What Enter inserts: the document's own line ending, the one the status bar
    /// shows, and with <paramref name="indent"/> the current line's leading whitespace.</summary>
    internal static string LineBreakAt(string text, int caret, bool indent) =>
        LineBreakAt(DetectLineEnding(text), text, caret, indent);

    /// <summary>The same, with the document's line ending already known.</summary>
    internal static string LineBreakAt(string ending, string text, int caret, bool indent)
    {
        var result = new StringBuilder(ending switch
        {
            "LF" => "\n",
            "CR" => "\r",
            _ => "\r\n",
        });

        if (indent)
        {
            int lineStart = caret == 0 ? 0 : text.LastIndexOfAny(['\n', '\r'], caret - 1) + 1;
            for (int i = lineStart; i < caret && (text[i] == ' ' || text[i] == '\t'); i++)
                result.Append(text[i]);
        }
        return result.ToString();
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            StepZoom(e.Delta > 0 ? +0.1 : -0.1);
            e.Handled = true;
        }
    }

    // ------------------------------------------------------ Drag & drop support

    private void Editor_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Editor_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            foreach (string file in files)
                OpenFile(file);
    }
}
