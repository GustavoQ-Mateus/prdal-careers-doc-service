using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocService;

static class PdfRenderer
{
    public static byte[] Render(IReadOnlyList<DocElement> elements)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                page.Content().Column(column =>
                {
                    column.Spacing(3);
                    foreach (var element in elements)
                    {
                        column.Item().Text(text => Style(text, element));
                    }
                });
            });
        }).GeneratePdf();
    }

    static void Style(QuestPDF.Fluent.TextDescriptor text, DocElement element)
    {
        switch (element.Kind)
        {
            case ElementKind.Heading1:
                text.Span(element.Text).FontSize(14).Bold();
                break;
            case ElementKind.Heading2:
                text.Span(element.Text).FontSize(11).Bold();
                break;
            case ElementKind.ListItem:
                text.Span($"\u2022 {element.Text}");
                break;
            default:
                text.Span(element.Text);
                break;
        }
    }
}
