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
        if (!_autoIndent || e.Key != Key.Return)
            return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) != 0)
            return;

        var ed = tab.Editor;
        string text = ed.Text;
        int start = ed.SelectionStart;
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;

        var indent = new StringBuilder();
        for (int i = lineStart; i < start && (text[i] == ' ' || text[i] == '\t'); i++)
            indent.Append(text[i]);

        ed.SelectedText = "\r\n" + indent;
        ed.CaretIndex = start + 2 + indent.Length;
        ed.SelectionLength = 0;
        e.Handled = true;
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
