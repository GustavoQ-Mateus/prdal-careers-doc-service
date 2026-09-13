using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocService;

static class DocxRenderer
{
    public static byte[] Render(IReadOnlyList<DocElement> elements)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            foreach (var element in elements)
            {
                body.AppendChild(Paragraph(element));
            }
        }
        return stream.ToArray();
    }

    static Paragraph Paragraph(DocElement element)
    {
        var run = new Run();
        var props = new RunProperties();

        switch (element.Kind)
        {
            case ElementKind.Heading1:
                props.Append(new Bold(), new FontSize { Val = "32" });
                break;
            case ElementKind.Heading2:
                props.Append(new Bold(), new FontSize { Val = "26" });
                break;
        }

        run.Append(props);
        var text = element.Kind == ElementKind.ListItem ? $"• {element.Text}" : element.Text;
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        return new Paragraph(run);
    }
}
