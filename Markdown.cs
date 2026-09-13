using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DocService;

enum ElementKind
{
    Heading1,
    Heading2,
    Paragraph,
    ListItem,
}

record DocElement(ElementKind Kind, string Text);

static class MarkdownParser
{
    public static List<DocElement> Parse(string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown ?? string.Empty);
        var elements = new List<DocElement>();
        foreach (var block in doc)
        {
            AppendBlock(block, elements);
        }
        return elements;
    }

    static void AppendBlock(Block block, List<DocElement> elements)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var kind = heading.Level <= 1 ? ElementKind.Heading1 : ElementKind.Heading2;
                elements.Add(new DocElement(kind, InlineText(heading.Inline)));
                break;
            case ParagraphBlock paragraph:
                elements.Add(new DocElement(ElementKind.Paragraph, InlineText(paragraph.Inline)));
                break;
            case ListBlock list:
                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        foreach (var inner in listItem)
                        {
                            if (inner is ParagraphBlock p)
                            {
                                elements.Add(new DocElement(ElementKind.ListItem, InlineText(p.Inline)));
                            }
                        }
                    }
                }
                break;
        }
    }

    static string InlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }
        var parts = new List<string>();
        foreach (var node in inline)
        {
            switch (node)
            {
                case LiteralInline literal:
                    parts.Add(literal.Content.ToString());
                    break;
                case LineBreakInline:
                    parts.Add(" ");
                    break;
                case ContainerInline container:
                    parts.Add(InlineText(container));
                    break;
            }
        }
        return string.Concat(parts).Trim();
    }
}
