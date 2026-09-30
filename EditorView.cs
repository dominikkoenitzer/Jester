using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace Jester;

/// <summary>
/// One document's editing surface: an AvalonEdit <see cref="TextEditor"/> paired with a
/// line-number gutter and a current-line highlight. Each open tab owns its own instance,
/// which is what gives every document an independent undo history and scroll position.
/// </summary>
internal sealed class EditorView : Grid
{
    private readonly LineNumberMargin _margin;
    private readonly Grid _textArea;
    private readonly Canvas _highlightLayer;
    private readonly Rectangle _currentLineHighlight;
    private bool _showLineNumbers = true;

    public TextEditor Editor { get; }

    /// <summary>Where each line of the editor's text starts, updated with every change.</summary>
    public LineIndex Lines { get; } = new();

    public EditorView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Editor = new TextEditor
        {
            WordWrap = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8),
            AllowDrop = true,
            Background = Brushes.Transparent,
        };
        Editor.SetResourceReference(Control.ForegroundProperty, "EditorForegroundBrush");
        Configure(Editor);
        Editor.ContextMenu = BuildContextMenu();

        var document = Editor.Document;
        // Changed comes before the editor's TextChanged, so every handler of that sees the
        // index already updated.
        document.Changed += (_, e) =>
            Lines.Apply(e.Offset, e.RemovalLength, e.InsertionLength, document.TextLength, document.GetText);
        Editor.TextChanged += (_, _) => OnTextChanged();

        _margin = new LineNumberMargin(Editor);
        SetColumn(_margin, 0);
        Children.Add(_margin);

        _textArea = new Grid();
        _textArea.SetResourceReference(BackgroundProperty, "EditorBackgroundBrush");
        SetColumn(_textArea, 1);

        _highlightLayer = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        _currentLineHighlight = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x24, 0xE8, 0xB5, 0x3D)),
            Height = 0,
        };
        _highlightLayer.Children.Add(_currentLineHighlight);

        _textArea.Children.Add(_highlightLayer);
        _textArea.Children.Add(Editor);
        Children.Add(_textArea);

        var textView = Editor.TextArea.TextView;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCurrentLine();
        textView.VisualLinesChanged += (_, _) => UpdateCurrentLine();
        textView.ScrollOffsetChanged += (_, _) => UpdateCurrentLine();
        _textArea.SizeChanged += (_, _) => UpdateCurrentLine();
        Loaded += (_, _) => UpdateCurrentLine();
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            _showLineNumbers = value;
            _margin.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>A stretch of the editor's text, read without copying the rest of it.</summary>
    public string Read(int start, int length) => Editor.Document.GetText(start, length);

    // Behaves and looks like the TextBox it replaced: no link underlines, no box
    // selection, copy and cut only with a selection, wrapped rows start at the margin,
    // and the theme's purple caret over a gold selection that keeps the text's colour.
    private static void Configure(TextEditor editor)
    {
        var options = editor.Options;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.EnableRectangularSelection = false;
        options.CutCopyWholeLine = false;
        options.InheritWordWrapIndentation = false;

        var area = editor.TextArea;
        area.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE8, 0xB5, 0x3D));
        area.SelectionForeground = null;
        area.SelectionBorder = null;
        area.SelectionCornerRadius = 0;
        area.Caret.CaretBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x1D, 0x6A));

        // AvalonEdit's Delete command only removes a selection. Without it the command
        // reaches the window, whose Delete also removes the next character.
        var editing = area.DefaultInputHandler.Editing;
        foreach (var binding in editing.CommandBindings.Where(b => b.Command == ApplicationCommands.Delete).ToList())
            editing.CommandBindings.Remove(binding);
    }

    private void OnTextChanged()
    {
        // The gutter sizes itself to the line count.
        _margin.TotalLines = Lines.Count;
        _margin.InvalidateMeasure();
        _margin.InvalidateVisual();
    }

    /// <summary>Re-measures and repaints the gutter after a font or zoom change.</summary>
    public void RefreshGutter()
    {
        _margin.InvalidateMeasure();
        _margin.InvalidateVisual();
        UpdateCurrentLine();
    }

    private void UpdateCurrentLine()
    {
        var area = Editor.TextArea;
        _margin.CurrentLine = Lines.LineAt(area.Caret.Offset);
        _margin.InvalidateVisual();

        // Only a line on screen has a row to highlight. The caret's row within it is the
        // one to mark, so a wrapped line lights up where the caret is, as before.
        var textView = area.TextView;
        var line = textView.VisualLinesValid ? textView.GetVisualLine(area.Caret.Line) : null;
        if (line is null || !IsAncestorOf(textView))
        {
            _currentLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }

        var caret = area.Caret.Position;
        var row = line.GetTextLine(caret.VisualColumn, caret.IsAtEndOfLine);
        double top = line.GetTextLineVisualYPosition(row, VisualYPosition.LineTop) - textView.VerticalOffset;

        _currentLineHighlight.Visibility = Visibility.Visible;
        Canvas.SetTop(_currentLineHighlight, textView.TranslatePoint(new Point(0, top), _textArea).Y);
        _currentLineHighlight.Width = _textArea.ActualWidth;
        _currentLineHighlight.Height = row.Height;
    }

    /// <summary>Builds the editor's themed right-click menu (the default WPF one is unstyled).</summary>
    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        MenuItem Item(ICommand command, string header)
        {
            var item = new MenuItem { Header = header, Command = command, CommandTarget = Editor.TextArea };
            return item;
        }

        menu.Items.Add(Item(ApplicationCommands.Cut, "Cu_t"));
        menu.Items.Add(Item(ApplicationCommands.Copy, "_Copy"));
        menu.Items.Add(Item(ApplicationCommands.Paste, "_Paste"));
        menu.Items.Add(Item(ApplicationCommands.Delete, "De_lete"));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(ApplicationCommands.SelectAll, "Select _All"));
        return menu;
    }
}
