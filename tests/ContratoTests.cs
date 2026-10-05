using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DocService.Tests;

public class ContratoTests
{
    [Fact]
    public async Task ContratoGeradoCorrespondeAoArquivo()
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("desenvolvimento", "");
        var resposta = await fabrica.CreateClient().GetAsync("/openapi/v1.json");
        resposta.EnsureSuccessStatusCode();
        var gerado = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        var arquivo = Path.Combine(fabrica.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "contrato", "openapi.json");
        if (Environment.GetEnvironmentVariable("GERAR_CONTRATO") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            await File.WriteAllTextAsync(arquivo, gerado.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        var esperado = JsonNode.Parse(await File.ReadAllTextAsync(arquivo));
        Assert.True(JsonNode.DeepEquals(esperado, gerado), "Contrato divergiu; execute gerar-contrato.ps1 e revise a alteracao");
        Assert.Equal(3, gerado["paths"]!.AsObject().Count);
        Assert.NotNull(gerado["paths"]!["/render/pdf"]!["post"]!["responses"]!["200"]!["headers"]!["X-Paginas"]);
    }

    [Fact]
    public async Task ProducaoNaoExpoeDocumentacao()
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token);
        var resposta = await ServicoAutenticadoTests.ClienteAutenticado(fabrica).GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
