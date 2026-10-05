namespace DocService;

public class Inicializacao
{
    public static void ConfigurarServicos(IServiceCollection servicos)
    {
        servicos.AddRouting();
    }

    public void ConfigureServices(IServiceCollection servicos) => ConfigurarServicos(servicos);

    public void Configure(IApplicationBuilder app, IConfiguration config) => Configurar(app, config);

    public static void Configurar(IApplicationBuilder app, IConfiguration config)
    {
        ServicoAutenticado.ExigirTokenNoBoot(config);
        app.UseServicoAutenticado(config);
        var limites = new LimitesRenderizacao(config);
        app.UseLimiteDeCorpo(limites);
        app.UseRouting();
        app.UseEndpoints(rotas => Rotas.Mapear(rotas, limites));
    }
}

static class Rotas
{
    public static void Mapear(IEndpointRouteBuilder rotas, LimitesRenderizacao limites)
    {
        rotas.MapGet("/health", () => new HealthResponse("doc-service", "ok"));
        rotas.MapPost("/render/docx", (RenderRequest req) =>
            limites.MarkdownExcede(req.Markdown)
                ? Task.FromResult(limites.CorpoGrande())
                : limites.Renderizar(
                    () => DocxRenderer.Render(MarkdownParser.Parse(req.Markdown), req.Template),
                    bytes => Results.File(bytes,
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "curriculo.docx")));
        rotas.MapPost("/render/pdf", (RenderRequest req, HttpContext context) =>
            limites.MarkdownExcede(req.Markdown)
                ? Task.FromResult(limites.CorpoGrande())
                : limites.Renderizar(
                    () => PdfRenderer.RenderComPaginas(MarkdownParser.Parse(req.Markdown), req.Template),
                    pdf =>
                    {
                        context.Response.Headers["X-Paginas"] = pdf.Paginas.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        return Results.File(pdf.Bytes, "application/pdf", "curriculo.pdf");
                    }));
    }
}

record HealthResponse(string Service, string Status);
record RenderRequest(string Markdown, string? Template);
