using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Microsoft.AspNetCore.Hosting;

namespace DocService.Tests;

public class LambdaTests
{
    sealed class LambdaDeTeste(string tempo = "15000") : Lambda
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseSetting("PRDAL_AMBIENTE", "producao");
            builder.UseSetting("SERVICE_TOKEN", ServicoAutenticadoTests.Token);
            builder.UseSetting("RENDER_TIMEOUT_MS", tempo);
            base.Init(builder);
        }
    }

    static APIGatewayHttpApiV2ProxyRequest Evento(string rota, string markdown, bool base64 = false) => new()
    {
        Version = "2.0",
        RawPath = rota,
        Headers = new Dictionary<string, string>
        {
            ["content-type"] = "application/json",
            ["X-Prdal-Servico"] = ServicoAutenticadoTests.Token,
        },
        Body = base64 ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { markdown })))
            : JsonSerializer.Serialize(new { markdown }),
        IsBase64Encoded = base64,
        RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
        {
            Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST", Path = rota, SourceIp = "127.0.0.1" },
        },
    };

    [Theory]
    [InlineData("/render/pdf")]
    [InlineData("/render/docx")]
    public async Task EventoRenderizaBinario(string rota)
    {
        var resposta = await new LambdaDeTeste().FunctionHandlerAsync(Evento(rota, "# Nome"), new TestLambdaContext());
        Assert.Equal(200, resposta.StatusCode);
        Assert.True(resposta.IsBase64Encoded);
        var bytes = Convert.FromBase64String(resposta.Body);
        Assert.True(bytes.Length > 100);
        if (rota.EndsWith("pdf"))
        {
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes));
            Assert.Equal("1", resposta.Headers["X-Paginas"]);
        }
        else Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    [Theory]
    [InlineData("/render/pdf", false)]
    [InlineData("/render/docx", false)]
    [InlineData("/render/pdf", true)]
    public async Task MarkdownDe8MbERejeitado(string rota, bool base64)
    {
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var resposta = await new LambdaDeTeste().FunctionHandlerAsync(Evento(rota, new string('x', 8 * 1024 * 1024), base64), new TestLambdaContext());
        Assert.Equal(413, resposta.StatusCode);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EventoSemCredencialERejeitado()
    {
        var evento = Evento("/render/pdf", "# Nome");
        evento.Headers.Remove("X-Prdal-Servico");
        var resposta = await new LambdaDeTeste().FunctionHandlerAsync(evento, new TestLambdaContext());
        Assert.Equal(401, resposta.StatusCode);
    }

    [Fact]
    public async Task RenderAlemDoPrazoERejeitado()
    {
        var resposta = await new LambdaDeTeste("1").FunctionHandlerAsync(
            Evento("/render/pdf", string.Join("\n", Enumerable.Repeat("- Desenvolvi integracoes em C# e SQL", 4000))), new TestLambdaContext());
        Assert.Equal(503, resposta.StatusCode);
    }
}
