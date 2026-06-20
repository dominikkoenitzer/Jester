using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Jester;

public partial class FontWindow : ThemedWindow
{
    private const double PointsToDip = 96.0 / 72.0;
    private static readonly double[] CommonSizes =
        { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

    public FontFamily SelectedFamily { get; private set; }
    public double SelectedSizePoints { get; private set; }
    public bool Bold { get; private set; }
    public bool Italic { get; private set; }

    public FontWindow(FontFamily family, double sizePoints, bool bold, bool italic)
    {
        InitializeComponent();

        SelectedFamily = family;
        SelectedSizePoints = sizePoints;
        Bold = bold;
        Italic = italic;

        FamilyList.ItemsSource = Fonts.SystemFontFamilies
            .OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FamilyList.SelectedItem = FamilyList.Items
            .Cast<FontFamily>()
            .FirstOrDefault(f => string.Equals(f.Source, family.Source, StringComparison.OrdinalIgnoreCase));
        if (FamilyList.SelectedItem is null && FamilyList.Items.Count > 0)
            FamilyList.SelectedIndex = 0;
        FamilyList.ScrollIntoView(FamilyList.SelectedItem);

        SizeList.ItemsSource = CommonSizes;
        SizeBox.Text = FormatSize(sizePoints);

        BoldBox.IsChecked = bold;
        ItalicBox.IsChecked = italic;

        UpdatePreview();
    }

    private void Selection_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void SizeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SizeList.SelectedItem is double size)
            SizeBox.Text = FormatSize(size);
    }

    private void SizeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (Preview is null)
            return;

        if (FamilyList.SelectedItem is FontFamily family)
            Preview.FontFamily = family;

        if (TryGetSize(out double points))
            Preview.FontSize = points * PointsToDip;

        Preview.FontWeight = BoldBox.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        Preview.FontStyle = ItalicBox.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
    }

    private bool TryGetSize(out double points)
    {
        if (double.TryParse(SizeBox.Text.Trim(), out points) && points is >= 1 and <= 512)
            return true;

        points = 0;
        return false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSize(out double points))
        {
            MessageBox.Show(this, "Please enter a font size between 1 and 512.",
                "Font", MessageBoxButton.OK, MessageBoxImage.Warning);
            SizeBox.SelectAll();
            SizeBox.Focus();
            return;
        }

        if (FamilyList.SelectedItem is FontFamily family)
            SelectedFamily = family;

        SelectedSizePoints = points;
        Bold = BoldBox.IsChecked == true;
        Italic = ItalicBox.IsChecked == true;
        DialogResult = true;
    }

    private static string FormatSize(double size) =>
        size == Math.Floor(size) ? ((int)size).ToString() : size.ToString();
}
