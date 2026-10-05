namespace DocService;

public class Inicializacao
{
    public static void ConfigurarServicos(IServiceCollection servicos)
    {
        servicos.AddRouting();
        servicos.AddEndpointsApiExplorer();
        servicos.AddOpenApi("v1", Contrato.Configurar);
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
        app.UseEndpoints(rotas =>
        {
            Rotas.Mapear(rotas, limites);
            if (ServicoAutenticado.EmDesenvolvimento(config)) rotas.MapOpenApi();
        });
    }
}

static class Rotas
{
    public static void Mapear(IEndpointRouteBuilder rotas, LimitesRenderizacao limites)
    {
        rotas.MapGet("/health", () => new HealthResponse("doc-service", "ok")).WithName("health");
        rotas.MapPost("/render/docx", (RenderRequest req) =>
            limites.MarkdownExcede(req.Markdown)
                ? Task.FromResult(limites.CorpoGrande())
                : limites.Renderizar(
                    () => DocxRenderer.Render(MarkdownParser.Parse(req.Markdown), req.Template),
                    bytes => Results.File(bytes,
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "curriculo.docx")))
            .WithName("renderDocx")
            .Produces<byte[]>(200, contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            .Produces<ErroResponse>(401).Produces<ErroResponse>(413).Produces<ErroResponse>(503);
        rotas.MapPost("/render/pdf", (RenderRequest req, HttpContext context) =>
            limites.MarkdownExcede(req.Markdown)
                ? Task.FromResult(limites.CorpoGrande())
                : limites.Renderizar(
                    () => PdfRenderer.RenderComPaginas(MarkdownParser.Parse(req.Markdown), req.Template),
                    pdf =>
                    {
                        context.Response.Headers["X-Paginas"] = pdf.Paginas.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        return Results.File(pdf.Bytes, "application/pdf", "curriculo.pdf");
                    }))
            .WithName("renderPdf")
            .Produces<byte[]>(200, contentType: "application/pdf")
            .Produces<ErroResponse>(401).Produces<ErroResponse>(413).Produces<ErroResponse>(503);
    }
}

record HealthResponse(string Service, string Status);
record RenderRequest(string Markdown, string? Template);
record ErroResponse(string Erro);
