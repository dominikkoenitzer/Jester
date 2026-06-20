using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Jester;

/// <summary>
/// One document's editing surface: a <see cref="TextBox"/> paired with a line-number
/// gutter and a current-line highlight. Each open tab owns its own instance, which is
/// what gives every document an independent undo history and scroll position.
/// </summary>
internal sealed class EditorView : Grid
{
    private readonly LineNumberMargin _margin;
    private readonly Grid _textArea;
    private readonly Canvas _highlightLayer;
    private readonly Rectangle _currentLineHighlight;
    private bool _showLineNumbers = true;

    public TextBox Editor { get; }

    public EditorView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8),
            AllowDrop = true,
            UndoLimit = -1,
            IsInactiveSelectionHighlightEnabled = true,
            Background = Brushes.Transparent,
        };
        Editor.SetResourceReference(Control.ForegroundProperty, "EditorForegroundBrush");
        SpellCheck.SetIsEnabled(Editor, false);
        Editor.ContextMenu = BuildContextMenu();

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

        Editor.SelectionChanged += (_, _) => UpdateCurrentLine();
        Editor.TextChanged += (_, _) => UpdateCurrentLine();
        Editor.AddHandler(ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, _) => UpdateCurrentLine()));
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

    /// <summary>Tells the gutter how many logical lines exist so it can size itself.</summary>
    public void SetTotalLines(int lines)
    {
        _margin.TotalLines = Math.Max(1, lines);
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
        int caret = Editor.CaretIndex;
        _margin.CurrentLine = LogicalLineAt(Editor.Text, caret);
        _margin.InvalidateVisual();

        Rect r = Editor.GetRectFromCharacterIndex(caret);
        if (r.IsEmpty)
        {
            _currentLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }

        _currentLineHighlight.Visibility = Visibility.Visible;
        Canvas.SetTop(_currentLineHighlight, r.Top);
        _currentLineHighlight.Width = _textArea.ActualWidth;
        _currentLineHighlight.Height = r.Height > 0 ? r.Height : Editor.FontSize * 1.3;
    }

    /// <summary>Builds the editor's themed right-click menu (the default WPF one is unstyled).</summary>
    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        MenuItem Item(ICommand command, string header)
        {
            var item = new MenuItem { Header = header, Command = command, CommandTarget = Editor };
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

    private static int LogicalLineAt(string text, int charIndex)
    {
        int line = 1;
        int limit = Math.Min(charIndex, text.Length);
        for (int i = 0; i < limit; i++)
            if (text[i] == '\n')
                line++;
        return line;
    }
}
