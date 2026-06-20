using System.Windows.Input;

namespace Jester;

/// <summary>
/// Custom routed commands for the actions WPF does not ship a built-in
/// <see cref="ApplicationCommands"/> entry for. Key gestures registered here are
/// shown automatically in menu items and fire via the window's command bindings.
/// </summary>
public static class JesterCommands
{
    public static readonly RoutedUICommand SaveAs = new(
        "Save _As...", nameof(SaveAs), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));

    public static readonly RoutedUICommand Exit = new(
        "E_xit", nameof(Exit), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.W, ModifierKeys.Control)));

    public static readonly RoutedUICommand FindNext = new(
        "Find _Next", nameof(FindNext), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.F3)));

    public static readonly RoutedUICommand FindPrevious = new(
        "Find _Previous", nameof(FindPrevious), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.F3, ModifierKeys.Shift)));

    public static readonly RoutedUICommand GoTo = new(
        "_Go To...", nameof(GoTo), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.G, ModifierKeys.Control)));

    public static readonly RoutedUICommand InsertDateTime = new(
        "Time/_Date", nameof(InsertDateTime), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.F5)));

    public static readonly RoutedUICommand ExportPdf = new(
        "Export to _PDF...", nameof(ExportPdf), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.E, ModifierKeys.Control | ModifierKeys.Shift)));

    public static readonly RoutedUICommand ChooseFont = new(
        "_Font...", nameof(ChooseFont), typeof(JesterCommands));

    public static readonly RoutedUICommand ZoomIn = new(
        "Zoom _In", nameof(ZoomIn), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.OemPlus, ModifierKeys.Control),
                 new KeyGesture(Key.Add, ModifierKeys.Control)));

    public static readonly RoutedUICommand ZoomOut = new(
        "Zoom _Out", nameof(ZoomOut), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.OemMinus, ModifierKeys.Control),
                 new KeyGesture(Key.Subtract, ModifierKeys.Control)));

    public static readonly RoutedUICommand RestoreZoom = new(
        "_Restore Default Zoom", nameof(RestoreZoom), typeof(JesterCommands),
        Gestures(new KeyGesture(Key.D0, ModifierKeys.Control),
                 new KeyGesture(Key.NumPad0, ModifierKeys.Control)));

    public static readonly RoutedUICommand About = new(
        "_About Jester", nameof(About), typeof(JesterCommands));

    private static InputGestureCollection Gestures(params KeyGesture[] gestures)
    {
        var collection = new InputGestureCollection();
        foreach (var gesture in gestures)
            collection.Add(gesture);
        return collection;
    }
}
