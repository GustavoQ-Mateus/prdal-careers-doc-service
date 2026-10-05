using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace DocService.Tests;

public class PaginasTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public async Task CabecalhoInformaPaginasDoDocumento(int linhas)
    {
        using var fabrica = ServicoAutenticadoTests.Fabrica("producao", ServicoAutenticadoTests.Token);
        var markdown = "# Nome\n\n" + string.Join("\n", Enumerable.Repeat("- Desenvolvi integracoes em C# e SQL", linhas));
        var resposta = await ServicoAutenticadoTests.ClienteAutenticado(fabrica).PostAsJsonAsync("/render/pdf", new { markdown });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var paginas = int.Parse(Assert.Single(resposta.Headers.GetValues("X-Paginas")));
        var pdf = Encoding.Latin1.GetString(await resposta.Content.ReadAsByteArrayAsync());
        Assert.Equal(Regex.Matches(pdf, @"/Type\s*/Page\b").Count, paginas);
        if (linhas == 1) Assert.Equal(1, paginas);
        else Assert.True(paginas > 1);
    }
}
