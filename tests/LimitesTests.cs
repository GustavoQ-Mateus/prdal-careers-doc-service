using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocService.Tests;

public class LimitesTests
{
    static string MarkdownDe(int bytes)
    {
        var linha = "- Desenvolvi integracoes com C#, .NET e SQL Server em ambiente de alta disponibilidade\n";
        var texto = new StringBuilder("# Nome\n\n## Experiencia\n\n");
        while (texto.Length < bytes) texto.Append(linha);
        return texto.ToString();
    }

    [Theory]
    [InlineData("/render/pdf")]
    [InlineData("/render/docx")]
    public async Task MarkdownDe8MbERejeitadoAntesDeRenderizar(string rota)
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token);
        var cliente = ServicoAutenticadoTests.ClienteAutenticado(fabrica);
        var relogio = Stopwatch.StartNew();
        var corpo = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { markdown = MarkdownDe(8 * 1024 * 1024) }),
            Encoding.UTF8,
            "application/json");
        Assert.True(corpo.Headers.ContentLength > 8 * 1024 * 1024);
        var resposta = await cliente.PostAsync(rota, corpo);
        relogio.Stop();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
        Assert.Contains("limite", await resposta.Content.ReadAsStringAsync());
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(5), $"levou {relogio.Elapsed}");
    }

    [Fact]
    public async Task MarkdownAcimaDe200KbEnviadoEmPartesTambemERejeitado()
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token);
        var cliente = ServicoAutenticadoTests.ClienteAutenticado(fabrica);
        var corpo = JsonContent.Create(new { markdown = MarkdownDe(210 * 1024) });
        Assert.Null(corpo.Headers.ContentLength);
        var resposta = await cliente.PostAsync("/render/pdf", corpo);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
    }

    [Fact]
    public async Task MarkdownDentroDoLimiteRenderiza()
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token);
        var cliente = ServicoAutenticadoTests.ClienteAutenticado(fabrica);
        var resposta = await cliente.PostAsJsonAsync("/render/docx", new { markdown = MarkdownDe(20 * 1024) });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task RenderizacaoAlemDoTempoMaximoRespondeErroClaro()
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token)
            .WithWebHostBuilder(builder => builder.UseSetting("RENDER_TIMEOUT_MS", "1"));
        var cliente = ServicoAutenticadoTests.ClienteAutenticado(fabrica);
        var resposta = await cliente.PostAsJsonAsync("/render/pdf", new { markdown = MarkdownDe(150 * 1024) });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Contains("tempo maximo", await resposta.Content.ReadAsStringAsync());
    }
}
