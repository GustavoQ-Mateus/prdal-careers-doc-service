using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocService;

static class DocxRenderer
{
    public static byte[] Render(IReadOnlyList<DocElement> elements, string? template = null)
    {
        var layout = ResumeLayout.From(template);
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            AddNumbering(main);
            var body = main.Document.AppendChild(new Body());
            foreach (var element in elements)
            {
                body.AppendChild(CreateParagraph(main, element, layout));
            }
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin
                {
                    Top = 700,
                    Right = 700,
                    Bottom = 700,
                    Left = 700,
                    Header = 708,
                    Footer = 708,
                    Gutter = 0,
                }));
        }
        return stream.ToArray();
    }

    static Paragraph CreateParagraph(
        MainDocumentPart main,
        DocElement element,
        ResumeLayout layout)
    {
        var paragraph = new Paragraph();
        var (before, after) = Spacing(element.Kind, layout);
        var props = new ParagraphProperties(new SpacingBetweenLines
        {
            Before = before.ToString(),
            After = after.ToString(),
        });

        if (element.Kind is ElementKind.Name or ElementKind.ProfessionalTitle or
            ElementKind.SectionHeading or ElementKind.JobRole or
            ElementKind.JobCompany or ElementKind.JobPeriod)
        {
            props.Append(new KeepNext());
        }
        if (element.Kind == ElementKind.SectionHeading)
        {
            props.Append(new ParagraphBorders(new BottomBorder
            {
                Val = BorderValues.Single,
                Size = 4,
                Space = 1,
                Color = "444444",
            }));
        }
        if (element.Kind == ElementKind.ListItem)
        {
            props.Append(
                new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 1 }),
                new Indentation { Left = "360", Hanging = "200" });
        }
        paragraph.Append(props);
        foreach (var span in element.Spans)
        {
            AppendSpan(main, paragraph, span, element.Kind, layout);
        }
        return paragraph;
    }

    static (int Before, int After) Spacing(ElementKind kind, ResumeLayout layout) => kind switch
    {
        ElementKind.Name => (0, layout.Tight(40)),
        ElementKind.ProfessionalTitle => (0, layout.Tight(55)),
        ElementKind.Contact => (0, layout.Tight(40)),
        ElementKind.SectionHeading => (layout.Tight(110), layout.Tight(45)),
        ElementKind.JobRole => (layout.Tight(80), 0),
        ElementKind.JobCompany => (0, 0),
        ElementKind.JobPeriod => (0, layout.Tight(35)),
        ElementKind.ListItem => (0, layout.Tight(16)),
        ElementKind.Skill => (0, layout.Tight(16)),
        ElementKind.EducationSchool => (0, layout.Tight(25)),
        _ => (0, layout.Tight(55)),
    };

    static void AppendSpan(
        MainDocumentPart main,
        Paragraph paragraph,
        DocSpan span,
        ElementKind kind,
        ResumeLayout layout)
    {
        if (!string.IsNullOrWhiteSpace(span.Url))
        {
            paragraph.Append(Hyperlink(main, span.Text, span.Url, span.Bold, kind, layout));
            return;
        }

        foreach (var (text, url) in LinkDetector.Split(span.Text))
        {
            paragraph.Append(url is null
                ? TextRun(text, span.Bold, kind, layout)
                : Hyperlink(main, text, url, span.Bold, kind, layout));
        }
    }

    static Run TextRun(string text, bool bold, ElementKind kind, ResumeLayout layout) =>
        new(BaseRunProperties(kind, layout, bold), new Text(text)
        {
            Space = SpaceProcessingModeValues.Preserve,
        });

    static Hyperlink Hyperlink(
        MainDocumentPart main,
        string text,
        string url,
        bool bold,
        ElementKind kind,
        ResumeLayout layout)
    {
        var relationship = main.AddHyperlinkRelationship(new Uri(url), true);
        var props = BaseRunProperties(kind, layout, bold);
        props.Append(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single });
        return new Hyperlink(new Run(props, new Text(text)
        {
            Space = SpaceProcessingModeValues.Preserve,
        }))
        {
            Id = relationship.Id,
        };
    }

    static RunProperties BaseRunProperties(
        ElementKind kind,
        ResumeLayout layout,
        bool inlineBold)
    {
        var size = kind switch
        {
            ElementKind.Name => layout.Size(32),
            ElementKind.ProfessionalTitle => layout.Size(21),
            ElementKind.Contact or ElementKind.JobPeriod => layout.Size(18),
            ElementKind.SectionHeading => layout.Size(22),
            _ => layout.Size(20),
        };
        var bold = inlineBold || kind is ElementKind.Name or ElementKind.SectionHeading or
            ElementKind.JobRole or ElementKind.EducationSchool;
        var props = new RunProperties(
            new RunFonts
            {
                Ascii = "Arial",
                HighAnsi = "Arial",
                ComplexScript = "Arial",
                EastAsia = "Arial",
            },
            new FontSize { Val = size.ToString() },
            new FontSizeComplexScript { Val = size.ToString() });
        if (bold) props.Append(new Bold(), new BoldComplexScript());
        if (kind is ElementKind.ProfessionalTitle or ElementKind.Contact)
        {
            props.Append(new Color { Val = "444444" });
        }
        if (kind == ElementKind.JobPeriod)
        {
            props.Append(new Color { Val = "666666" });
        }
        return props;
    }

    static void AddNumbering(MainDocumentPart main)
    {
        var part = main.AddNewPart<NumberingDefinitionsPart>();
        var level = new Level(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = NumberFormatValues.Bullet },
            new LevelText { Val = "•" },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = "360", Hanging = "200" }))
        {
            LevelIndex = 0,
        };
        var abstractNumber = new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.HybridMultilevel },
            level)
        {
            AbstractNumberId = 0,
        };
        var instance = new NumberingInstance(new AbstractNumId { Val = 0 })
        {
            NumberID = 1,
        };
        part.Numbering = new Numbering(abstractNumber, instance);
    }
}

sealed record ResumeLayout(int FontDelta, bool Compact)
{
    public static ResumeLayout From(string? template) =>
        string.Equals(template, "compact", StringComparison.OrdinalIgnoreCase)
            ? new ResumeLayout(2, true)
            : new ResumeLayout(0, false);

    public int Size(int halfPoints) => Math.Max(18, halfPoints - FontDelta);
    public int Tight(int value) => Compact ? Math.Max(0, value - 12) : value;
}
