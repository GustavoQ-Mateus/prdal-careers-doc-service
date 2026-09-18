using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DocService;

static class PdfRenderer
{
    public static byte[] Render(IReadOnlyList<DocElement> elements, string? template = null)
    {
        var compact = string.Equals(template, "compact", StringComparison.OrdinalIgnoreCase);
        var baseSize = compact ? 9f : 10f;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(baseSize));
                page.Content().Column(column =>
                {
                    column.Spacing(0);
                    foreach (var element in elements)
                    {
                        column.Item().Element(item => RenderElement(item, element, compact));
                    }
                });
            });
        }).GeneratePdf();
    }

    static void RenderElement(IContainer container, DocElement element, bool compact)
    {
        var (before, after) = Spacing(element.Kind, compact);
        var content = container.PaddingTop(before).PaddingBottom(after);
        if (element.Kind == ElementKind.SectionHeading)
        {
            content.BorderBottom(0.5f).BorderColor("444444").PaddingBottom(1).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(compact ? 10f : 11f).Bold());
                AppendSpans(text, element.Spans, true);
            });
            return;
        }
        if (element.Kind == ElementKind.ListItem)
        {
            content.Row(row =>
            {
                row.ConstantItem(9).Text("•");
                row.RelativeItem().Text(text => AppendSpans(text, element.Spans, false));
            });
            return;
        }
        content.Text(text =>
        {
            text.DefaultTextStyle(style => Style(style, element.Kind, compact));
            AppendSpans(text, element.Spans, IsBold(element.Kind));
        });
    }

    static TextStyle Style(TextStyle style, ElementKind kind, bool compact)
    {
        var result = kind switch
        {
            ElementKind.Name => style.FontSize(compact ? 15f : 16f).Bold(),
            ElementKind.ProfessionalTitle => style.FontSize(compact ? 9.5f : 10.5f).FontColor("333333"),
            ElementKind.Contact => style.FontSize(compact ? 8.5f : 9f).FontColor("444444"),
            ElementKind.JobPeriod => style.FontSize(compact ? 8.5f : 9f).FontColor("666666"),
            _ => style,
        };
        return IsBold(kind) ? result.Bold() : result;
    }

    static bool IsBold(ElementKind kind) => kind is ElementKind.Name or
        ElementKind.SectionHeading or ElementKind.JobRole or ElementKind.EducationSchool;

    static void AppendSpans(
        QuestPDF.Fluent.TextDescriptor text,
        IReadOnlyList<DocSpan> spans,
        bool forceBold)
    {
        foreach (var span in spans)
        {
            if (!string.IsNullOrWhiteSpace(span.Url))
            {
                AppendLink(text, span.Text, span.Url, forceBold || span.Bold);
                continue;
            }
            foreach (var (segment, url) in LinkDetector.Split(span.Text))
            {
                if (url is null)
                {
                    var descriptor = text.Span(segment);
                    if (forceBold || span.Bold) descriptor.Bold();
                }
                else
                {
                    AppendLink(text, segment, url, forceBold || span.Bold);
                }
            }
        }
    }

    static void AppendLink(QuestPDF.Fluent.TextDescriptor text, string segment, string url, bool bold)
    {
        var descriptor = text.Hyperlink(segment, url).FontColor("0563C1").Underline();
        if (bold) descriptor.Bold();
    }

    static (float Before, float After) Spacing(ElementKind kind, bool compact)
    {
        var factor = compact ? 0.72f : 1f;
        var spacing = kind switch
        {
            ElementKind.Name => (0f, 1.2f),
            ElementKind.ProfessionalTitle => (0f, 1.8f),
            ElementKind.Contact => (0f, 1.3f),
            ElementKind.SectionHeading => (3.2f, 1.3f),
            ElementKind.JobRole => (2.2f, 0f),
            ElementKind.JobCompany => (0f, 0f),
            ElementKind.JobPeriod => (0f, 0.8f),
            ElementKind.ListItem => (0f, 0.45f),
            ElementKind.Skill => (0f, 0.45f),
            ElementKind.EducationSchool => (0f, 0.7f),
            _ => (0f, 1.2f),
        };
        return (spacing.Item1 * factor, spacing.Item2 * factor);
    }
}
