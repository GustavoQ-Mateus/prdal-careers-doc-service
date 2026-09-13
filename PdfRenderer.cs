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
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
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
                text.Span(element.Text).FontSize(20).Bold();
                break;
            case ElementKind.Heading2:
                text.Span(element.Text).FontSize(15).Bold();
                break;
            case ElementKind.ListItem:
                text.Span($"• {element.Text}");
                break;
            default:
                text.Span(element.Text);
                break;
        }
    }
}
