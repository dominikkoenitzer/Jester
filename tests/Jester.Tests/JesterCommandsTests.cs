using System.Reflection;
using System.Windows.Input;
using Xunit;

namespace Jester.Tests;

/// <summary>
/// The custom command table. Two commands sharing a key gesture is a silent
/// failure — WPF binds whichever it reaches first and the other shortcut simply
/// stops working, with no build error and nothing visible in the menus.
/// </summary>
public class JesterCommandsTests
{
    private static IReadOnlyList<(string Name, RoutedUICommand Command)> AllCommands() =>
        typeof(JesterCommands)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(RoutedUICommand))
            .Select(f => (f.Name, (RoutedUICommand)f.GetValue(null)!))
            .ToList();

    private static IEnumerable<KeyGesture> GesturesOf(RoutedUICommand command) =>
        command.InputGestures.OfType<KeyGesture>();

    private static string Describe(KeyGesture g) => $"{g.Modifiers}+{g.Key}";

    [Fact]
    public void EveryCommandIsDiscoverable()
    {
        Assert.NotEmpty(AllCommands());
    }

    [Fact]
    public void EveryCommandHasATextLabelAndAnOwningType()
    {
        foreach (var (name, command) in AllCommands())
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Text), $"{name} has no menu text");
            Assert.Equal(typeof(JesterCommands), command.OwnerType);
        }
    }

    [Fact]
    public void EveryCommandNameMatchesItsFieldName()
    {
        // The commands pass nameof(...) as their Name; a copy-paste slip would
        // leave two commands claiming the same identity.
        foreach (var (name, command) in AllCommands())
            Assert.Equal(name, command.Name);
    }

    [Fact]
    public void NoTwoCommandsShareAKeyGesture()
    {
        var seen = new Dictionary<string, string>();

        foreach (var (name, command) in AllCommands())
        {
            foreach (var gesture in GesturesOf(command))
            {
                string key = Describe(gesture);
                Assert.False(
                    seen.TryGetValue(key, out string? owner),
                    $"{key} is bound to both {owner} and {name}");
                seen[key] = name;
            }
        }
    }

    [Theory]
    [InlineData(nameof(JesterCommands.SaveAs), Key.S, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(nameof(JesterCommands.SaveAll), Key.S, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(nameof(JesterCommands.CloseTab), Key.W, ModifierKeys.Control)]
    [InlineData(nameof(JesterCommands.Exit), Key.Q, ModifierKeys.Control)]
    [InlineData(nameof(JesterCommands.NextTab), Key.Tab, ModifierKeys.Control)]
    [InlineData(nameof(JesterCommands.PreviousTab), Key.Tab, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(nameof(JesterCommands.FindNext), Key.F3, ModifierKeys.None)]
    [InlineData(nameof(JesterCommands.FindPrevious), Key.F3, ModifierKeys.Shift)]
    [InlineData(nameof(JesterCommands.GoTo), Key.G, ModifierKeys.Control)]
    [InlineData(nameof(JesterCommands.FindInFiles), Key.F, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(nameof(JesterCommands.InsertDateTime), Key.F5, ModifierKeys.None)]
    [InlineData(nameof(JesterCommands.ExportPdf), Key.E, ModifierKeys.Control | ModifierKeys.Shift)]
    public void TheDocumentedShortcutsAreTheOnesActuallyBound(string name, Key key, ModifierKeys modifiers)
    {
        var (_, command) = AllCommands().Single(c => c.Name == name);
        Assert.Contains(GesturesOf(command), g => g.Key == key && g.Modifiers == modifiers);
    }

    [Theory]
    [InlineData(nameof(JesterCommands.ZoomIn), Key.OemPlus, Key.Add)]
    [InlineData(nameof(JesterCommands.ZoomOut), Key.OemMinus, Key.Subtract)]
    [InlineData(nameof(JesterCommands.RestoreZoom), Key.D0, Key.NumPad0)]
    public void ZoomIsBoundOnBothTheMainRowAndTheNumpad(string name, Key main, Key numpad)
    {
        var (_, command) = AllCommands().Single(c => c.Name == name);
        var keys = GesturesOf(command).Where(g => g.Modifiers == ModifierKeys.Control).Select(g => g.Key).ToList();

        Assert.Contains(main, keys);
        Assert.Contains(numpad, keys);
    }

    [Fact]
    public void ChooseFontIsMenuOnly()
    {
        // Deliberately unbound: it opens a dialog and there is no conventional
        // shortcut for it. Asserted so it is not given one by accident.
        Assert.Empty(GesturesOf(JesterCommands.ChooseFont));
    }
}
