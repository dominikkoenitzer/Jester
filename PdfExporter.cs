using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jester;

/// <summary>Renders the editor's text to a paginated A4 PDF document.</summary>
internal static class PdfExporter
{
    static PdfExporter()
    {
        // QuestPDF Community licence — free for individuals and small businesses.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void Export(string path, string text, string documentTitle, string fontFamily, double fontSizePoints)
    {
        string family = string.IsNullOrWhiteSpace(fontFamily) ? "Consolas" : fontFamily;
        float size = (float)Math.Clamp(fontSizePoints, 6, 24);

        // Map the editor's logical lines onto PDF lines; long lines wrap to the page.
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily(family).FontSize(size).LineHeight(1.35f));

                page.Header()
                    .PaddingBottom(6)
                    .BorderBottom(1).BorderColor("#E8B53D")
                    .PaddingBottom(6)
                    .Text(documentTitle)
                    .SemiBold().FontSize(size + 2).FontColor("#4A1D6A");

                page.Content().PaddingVertical(10).Column(column =>
                {
                    foreach (string line in lines)
                        column.Item().Text(line.Length == 0 ? " " : line);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);
    }
}
