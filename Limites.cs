using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace DocService;

sealed class LimitesRenderizacao
{
    public const int MarkdownPadraoBytes = 200 * 1024;
    public const int TempoPadraoMs = 15000;
    const int FolgaJsonBytes = 16 * 1024;

    public int MarkdownMaxBytes { get; }
    public int TempoMaxMs { get; }
    public long CorpoMaxBytes => (long)MarkdownMaxBytes * 2 + FolgaJsonBytes;

    public LimitesRenderizacao(IConfiguration config)
    {
        MarkdownMaxBytes = Inteiro(config["RENDER_MAX_MARKDOWN_BYTES"], MarkdownPadraoBytes);
        TempoMaxMs = Inteiro(config["RENDER_TIMEOUT_MS"], TempoPadraoMs);
    }

    static int Inteiro(string? valor, int padrao) =>
        int.TryParse(valor, out var numero) && numero > 0 ? numero : padrao;

    public IResult CorpoGrande() => Results.Json(
        new { erro = $"markdown acima do limite de {MarkdownMaxBytes / 1024} KB" },
        statusCode: StatusCodes.Status413PayloadTooLarge);

    public bool MarkdownExcede(string? markdown) =>
        Encoding.UTF8.GetByteCount(markdown ?? string.Empty) > MarkdownMaxBytes;

    public async Task<IResult> Renderizar<T>(Func<T> renderizar, Func<T, IResult> resposta)
    {
        var tarefa = Task.Run(renderizar);
        var concluida = await Task.WhenAny(tarefa, Task.Delay(TempoMaxMs));
        if (concluida != tarefa)
        {
            return Results.Json(
                new { erro = $"renderizacao excedeu o tempo maximo de {TempoMaxMs} ms" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return resposta(await tarefa);
    }
}

static class LimiteDeCorpo
{
    public static IApplicationBuilder UseLimiteDeCorpo(this IApplicationBuilder app, LimitesRenderizacao limites)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/render"))
            {
                await next(context);
                return;
            }
            if (context.Request.ContentLength > limites.CorpoMaxBytes)
            {
                await limites.CorpoGrande().ExecuteAsync(context);
                return;
            }
            var recurso = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (recurso is { IsReadOnly: false }) recurso.MaxRequestBodySize = limites.CorpoMaxBytes;
            await next(context);
        });
    }
}
