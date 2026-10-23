using Xunit;

namespace Jester.Tests;

/// <summary>Save As starts where the file already is, so a copy lands next to it unless the user moves on.</summary>
public class SaveAsDialogTests
{
    [Fact]
    public void SaveAsOpensInTheFilesOwnFolder() => StaThread.Run(() =>
    {
        var tab = new DocumentTab { FilePath = @"C:\Notes\2026\plan.txt" };

        var dialog = MainWindow.SaveAsDialog(tab);

        Assert.Equal(@"C:\Notes\2026", dialog.InitialDirectory);
        Assert.Equal("plan.txt", dialog.FileName);
    });

    [Fact]
    public void SaveAsForANewTabKeepsTheDialogsOwnFolder() => StaThread.Run(() =>
    {
        var tab = new DocumentTab();

        var dialog = MainWindow.SaveAsDialog(tab);

        Assert.Equal("", dialog.InitialDirectory);
        Assert.Equal(tab.UntitledName + ".txt", dialog.FileName);
    });
}
