using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocService.Tests;

public class ServicoAutenticadoTests
{
    internal const string Token = "token-de-servico-de-teste-com-mais-de-32-bytes";

    internal static WebApplicationFactory<Program> Fabrica(string ambiente, string token) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PRDAL_AMBIENTE", ambiente);
            builder.UseSetting("SERVICE_TOKEN", token);
        });

    internal static HttpClient ClienteAutenticado(WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add(ServicoAutenticado.Header, Token);
        return cliente;
    }

    [Theory]
    [InlineData("/render/pdf")]
    [InlineData("/render/docx")]
    public async Task RenderSemHeaderRetorna401(string rota)
    {
        using var fabrica = Fabrica("producao", Token);
        var resposta = await fabrica.CreateClient().PostAsJsonAsync(rota, new { markdown = "# Nome" });
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("errado")]
    [InlineData(Token + "x")]
    public async Task TokenErradoRetorna401(string token)
    {
        using var fabrica = Fabrica("producao", Token);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add(ServicoAutenticado.Header, token);
        var resposta = await cliente.PostAsJsonAsync("/render/pdf", new { markdown = "# Nome" });
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task ComHeaderCorretoRenderiza()
    {
        using var fabrica = Fabrica("producao", Token);
        var resposta = await ClienteAutenticado(fabrica).PostAsJsonAsync("/render/pdf", new { markdown = "# Nome" });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HealthContinuaPublico()
    {
        using var fabrica = Fabrica("producao", Token);
        var resposta = await fabrica.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Theory]
    [InlineData("producao", "")]
    [InlineData("producao", "curto")]
    [InlineData("", "")]
    public void ForaDeDesenvolvimentoNaoSobeSemToken(string ambiente, string token)
    {
        using var fabrica = Fabrica(ambiente, token);
        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());
        Assert.Contains("SERVICE_TOKEN", erro.ToString());
    }

    [Fact]
    public async Task EmDesenvolvimentoSobeSemToken()
    {
        using var fabrica = Fabrica("desenvolvimento", "");
        var resposta = await fabrica.CreateClient().PostAsJsonAsync("/render/pdf", new { markdown = "# Nome" });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
