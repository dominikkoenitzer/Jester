using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Jester;

public partial class FindInFilesWindow : ThemedWindow
{
    private readonly MainWindow _owner;

    public FindInFilesWindow(MainWindow owner, string? initialFolder, string? initialSearch)
    {
        InitializeComponent();
        _owner = owner;

        if (!string.IsNullOrEmpty(initialFolder))
            FolderBox.Text = initialFolder;
        if (!string.IsNullOrEmpty(initialSearch))
            FindBox.Text = initialSearch;

        Loaded += (_, _) =>
        {
            FindBox.Focus();
            FindBox.SelectAll();
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to search" };
        if (Directory.Exists(FolderBox.Text))
            dialog.InitialDirectory = FolderBox.Text;

        if (dialog.ShowDialog(this) == true)
            FolderBox.Text = dialog.FolderName;
    }

    private void FindAll_Click(object sender, RoutedEventArgs e)
    {
        string search = FindBox.Text;
        string folder = FolderBox.Text.Trim();

        if (search.Length == 0)
        {
            MessageBox.Show(this, "Enter the text to find.",
                "Find in Files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Choose an existing folder to search.",
                "Find in Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _owner.FindInFiles(search, folder, FilterBox.Text,
            MatchCaseBox.IsChecked == true, SubfoldersBox.IsChecked == true);
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
