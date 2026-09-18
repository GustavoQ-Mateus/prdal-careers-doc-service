using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocService;

enum ElementKind
{
    Name,
    ProfessionalTitle,
    Contact,
    SectionHeading,
    Paragraph,
    ListItem,
    JobRole,
    JobCompany,
    JobPeriod,
    Skill,
    EducationSchool,
    EducationDetail,
}

record DocSpan(string Text, bool Bold = false, string? Url = null);

record DocElement(ElementKind Kind, IReadOnlyList<DocSpan> Spans)
{
    public string Text => string.Concat(Spans.Select(span => span.Text)).Trim();
}

static partial class MarkdownParser
{
    [GeneratedRegex(@"^(.+?)\s*\|\s*(.+?)\s*\|\s*(.+?)$")]
    private static partial Regex JobHeaderRegex();

    public static List<DocElement> Parse(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown ?? string.Empty);
        var elements = new List<DocElement>();
        var state = new ParserState();
        foreach (var block in document)
        {
            AppendBlock(block, elements, state);
        }
        return elements;
    }

    static void AppendBlock(Block block, List<DocElement> elements, ParserState state)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var spans = InlineSpans(heading.Inline);
                if (heading.Level == 1 && !state.NameSeen)
                {
                    elements.Add(new DocElement(ElementKind.Name, spans));
                    state.NameSeen = true;
                }
                else
                {
                    state.Section = Normalize(string.Concat(spans.Select(span => span.Text)));
                    state.EducationStarted = false;
                    elements.Add(new DocElement(ElementKind.SectionHeading, spans));
                }
                break;
            case ParagraphBlock paragraph:
                AppendParagraph(paragraph, elements, state, false);
                break;
            case ListBlock list:
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    foreach (var inner in item.OfType<ParagraphBlock>())
                    {
                        AppendParagraph(inner, elements, state, true);
                    }
                }
                break;
        }
    }

    static void AppendParagraph(
        ParagraphBlock paragraph,
        List<DocElement> elements,
        ParserState state,
        bool fromList)
    {
        var spans = InlineSpans(paragraph.Inline);
        var text = string.Concat(spans.Select(span => span.Text)).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (state.Section.Contains("FORMAC") || state.Section.Contains("EDUC"))
        {
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kind = state.EducationStarted ? ElementKind.EducationDetail : ElementKind.EducationSchool;
                elements.Add(TextElement(kind, line));
                state.EducationStarted = true;
            }
            return;
        }

        spans = spans
            .Select(span => span with { Text = span.Text.Replace('\n', ' ') })
            .ToList();
        text = string.Concat(spans.Select(span => span.Text)).Trim();

        if (string.IsNullOrEmpty(state.Section))
        {
            if (!state.TitleSeen && spans.All(span => string.IsNullOrWhiteSpace(span.Text) || span.Bold))
            {
                elements.Add(new DocElement(ElementKind.ProfessionalTitle, spans));
                state.TitleSeen = true;
                return;
            }
            if (!state.ContactSeen)
            {
                elements.Add(new DocElement(ElementKind.Contact, spans));
                state.ContactSeen = true;
                return;
            }
        }

        if (state.Section.Contains("EXPERI"))
        {
            var match = JobHeaderRegex().Match(text);
            if (!fromList && match.Success)
            {
                elements.Add(TextElement(ElementKind.JobRole, match.Groups[2].Value));
                elements.Add(TextElement(ElementKind.JobCompany, match.Groups[1].Value));
                elements.Add(TextElement(ElementKind.JobPeriod, match.Groups[3].Value));
                return;
            }
            elements.Add(new DocElement(fromList ? ElementKind.ListItem : ElementKind.Paragraph, spans));
            return;
        }

        if (state.Section.Contains("COMPET") || state.Section.Contains("HABIL") || state.Section.Contains("SKILL"))
        {
            var colon = text.IndexOf(':');
            if (colon > 0)
            {
                elements.Add(new DocElement(ElementKind.Skill, new[]
                {
                    new DocSpan(text[..colon].Trim(), true),
                    new DocSpan(": "),
                    new DocSpan(text[(colon + 1)..].Trim()),
                }));
            }
            else
            {
                elements.Add(new DocElement(ElementKind.Paragraph, spans));
            }
            return;
        }

        elements.Add(new DocElement(fromList ? ElementKind.ListItem : ElementKind.Paragraph, spans));
    }

    static DocElement TextElement(ElementKind kind, string text) =>
        new(kind, new[] { new DocSpan(text.Trim()) });

    static List<DocSpan> InlineSpans(ContainerInline? inline, bool inheritedBold = false)
    {
        var spans = new List<DocSpan>();
        if (inline is null) return spans;
        foreach (var node in inline)
        {
            switch (node)
            {
                case LiteralInline literal:
                    AppendSpan(spans, literal.Content.ToString(), inheritedBold, null);
                    break;
                case LineBreakInline:
                    AppendSpan(spans, "\n", inheritedBold, null);
                    break;
                case LinkInline link:
                    foreach (var span in InlineSpans(link, inheritedBold))
                    {
                        AppendSpan(spans, span.Text, span.Bold, link.Url);
                    }
                    break;
                case EmphasisInline emphasis:
                    foreach (var span in InlineSpans(emphasis, inheritedBold || emphasis.DelimiterCount >= 2))
                    {
                        AppendSpan(spans, span.Text, span.Bold, span.Url);
                    }
                    break;
                case ContainerInline container:
                    foreach (var span in InlineSpans(container, inheritedBold))
                    {
                        AppendSpan(spans, span.Text, span.Bold, span.Url);
                    }
                    break;
            }
        }
        return spans;
    }

    static void AppendSpan(List<DocSpan> spans, string text, bool bold, string? url)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (spans.Count > 0 && spans[^1].Bold == bold && spans[^1].Url == url)
        {
            spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            return;
        }
        spans.Add(new DocSpan(text, bold, url));
    }

    static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }
        return builder.ToString();
    }

    sealed class ParserState
    {
        public bool NameSeen { get; set; }
        public bool TitleSeen { get; set; }
        public bool ContactSeen { get; set; }
        public bool EducationStarted { get; set; }
        public string Section { get; set; } = string.Empty;
    }
}

static partial class LinkDetector
{
    [GeneratedRegex(@"(https?://\S+|www\.\S+|(?:linkedin|github)\.com/\S+|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,})")]
    private static partial Regex Pattern();

    public static IEnumerable<(string Text, string? Url)> Split(string text)
    {
        var cursor = 0;
        foreach (Match match in Pattern().Matches(text))
        {
            if (match.Index > cursor)
            {
                yield return (text[cursor..match.Index], null);
            }
            yield return (match.Value, ResolveUrl(match.Value));
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length)
        {
            yield return (text[cursor..], null);
        }
    }

    static string ResolveUrl(string text) =>
        text.Contains('@') && !text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? $"mailto:{text}"
            : text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? text
                : $"https://{text}";
}
