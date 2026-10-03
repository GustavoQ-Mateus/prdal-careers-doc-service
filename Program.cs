using DocService;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();
ServicoAutenticado.ExigirTokenNoBoot(app.Configuration);
app.UseServicoAutenticado();
var limites = new LimitesRenderizacao(app.Configuration);
app.UseLimiteDeCorpo(limites);

app.MapGet("/health", () => new HealthResponse("doc-service", "ok"));

app.MapGet("/hello", () =>
{
    var hop = new HelloHop("doc-service", "hello from doc-service");
    return new HelloResponse("doc-service", hop.Message, new[] { hop });
});

app.MapPost("/render/docx", (RenderRequest req) =>
    limites.MarkdownExcede(req.Markdown)
        ? Task.FromResult(limites.CorpoGrande())
        : limites.Renderizar(
            () => DocxRenderer.Render(MarkdownParser.Parse(req.Markdown), req.Template),
            bytes => Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "curriculo.docx")));

app.MapPost("/render/pdf", (RenderRequest req) =>
    limites.MarkdownExcede(req.Markdown)
        ? Task.FromResult(limites.CorpoGrande())
        : limites.Renderizar(
            () => PdfRenderer.Render(MarkdownParser.Parse(req.Markdown), req.Template),
            bytes => Results.File(bytes, "application/pdf", "curriculo.pdf")));

app.Run();

public partial class Program;

record HealthResponse(string Service, string Status);
record HelloHop(string Service, string Message);
record HelloResponse(string Service, string Message, HelloHop[] Chain);
record RenderRequest(string Markdown, string? Template);
