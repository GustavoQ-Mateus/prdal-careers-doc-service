using System.Security.Cryptography;
using System.Text;

namespace DocService;

sealed class TokenServicoAusenteException(string message) : Exception(message);

static class ServicoAutenticado
{
    public const string Header = "X-Prdal-Servico";
    public const int TamanhoMinimoToken = 32;
    static readonly HashSet<string> RotasPublicas = new(StringComparer.OrdinalIgnoreCase) { "/health" };

    public static bool EmDesenvolvimento(IConfiguration config) =>
        string.Equals(config["PRDAL_AMBIENTE"]?.Trim(), "desenvolvimento", StringComparison.OrdinalIgnoreCase);

    public static string Token(IConfiguration config) => config["SERVICE_TOKEN"]?.Trim() ?? string.Empty;

    public static void ExigirTokenNoBoot(IConfiguration config)
    {
        if (EmDesenvolvimento(config)) return;
        if (Encoding.UTF8.GetByteCount(Token(config)) < TamanhoMinimoToken)
        {
            throw new TokenServicoAusenteException(
                $"SERVICE_TOKEN ausente ou com menos de {TamanhoMinimoToken} bytes; defina o token de servico antes de subir o doc-service fora de PRDAL_AMBIENTE=desenvolvimento");
        }
    }

    public static bool CredencialValida(IConfiguration config, string? recebido)
    {
        var esperado = Token(config);
        if (esperado.Length == 0) return EmDesenvolvimento(config);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recebido ?? string.Empty),
            Encoding.UTF8.GetBytes(esperado));
    }

    public static IApplicationBuilder UseServicoAutenticado(this WebApplication app)
    {
        var config = app.Configuration;
        return app.Use(async (context, next) =>
        {
            if (RotasPublicas.Contains(context.Request.Path.Value ?? string.Empty)
                || CredencialValida(config, context.Request.Headers[Header].ToString()))
            {
                await next(context);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { erro = "credencial de servico invalida" });
        });
    }
}
