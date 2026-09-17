using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace DocService;

static class DocxRenderer
{
    static readonly Regex LinkRegex = new(@"(https?://\S+|www\.\S+|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,})", RegexOptions.Compiled);

    public static byte[] Render(IReadOnlyList<DocElement> elements)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());
            var section = new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 700, Right = 700, Bottom = 700, Left = 700 });

            foreach (var element in elements)
            {
                body.AppendChild(Paragraph(main, element));
            }
            body.AppendChild(section);
        }
        return stream.ToArray();
    }

    static Paragraph Paragraph(MainDocumentPart main, DocElement element)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = "0",
                After = element.Kind == ElementKind.Heading1 ? "80" : "40"
            });

        if (element.Kind is ElementKind.Heading1 or ElementKind.Heading2)
        {
            props.Append(new KeepNext());
        }
        if (element.Kind == ElementKind.ListItem)
        {
            props.Append(new Indentation { Left = "360", Hanging = "200" });
        }

        paragraph.Append(props);
        var text = element.Kind == ElementKind.ListItem ? $"\u2022 {element.Text}" : element.Text;
        AppendRuns(main, paragraph, text, element.Kind);
        return paragraph;
    }

    static void AppendRuns(MainDocumentPart main, Paragraph paragraph, string text, ElementKind kind)
    {
        var cursor = 0;
        foreach (Match match in LinkRegex.Matches(text))
        {
            if (match.Index > cursor)
            {
                paragraph.Append(TextRun(text[cursor..match.Index], kind));
            }
            paragraph.Append(Hyperlink(main, match.Value, kind));
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length)
        {
            paragraph.Append(TextRun(text[cursor..], kind));
        }
    }

    static Run TextRun(string text, ElementKind kind)
    {
        return new Run(
            BaseRunProperties(kind),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static Hyperlink Hyperlink(MainDocumentPart main, string text, ElementKind kind)
    {
        var target = text.Contains('@') && !text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? $"mailto:{text}"
            : text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? text
                : $"https://{text}";
        var rel = main.AddHyperlinkRelationship(new Uri(target), true);
        var props = BaseRunProperties(kind);
        props.Append(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single });
        return new Hyperlink(new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve }))
        {
            Id = rel.Id
        };
    }

    static RunProperties BaseRunProperties(ElementKind kind)
    {
        var props = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
            new FontSize { Val = "20" });
        switch (kind)
        {
            case ElementKind.Heading1:
                props.Append(new Bold(), new FontSize { Val = "28" });
                break;
            case ElementKind.Heading2:
                props.Append(new Bold(), new FontSize { Val = "22" });
                break;
        }
        return props;
    }
}
