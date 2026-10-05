using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DocService;

static class Contrato
{
    public static void Configurar(OpenApiOptions opcoes)
    {
        opcoes.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
        opcoes.AddDocumentTransformer((documento, contexto, cancelamento) =>
        {
            documento.Info = new OpenApiInfo { Title = "doc-service", Version = "1.0.0" };
            documento.Servers = [];
            documento.Components ??= new OpenApiComponents();
            documento.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["tokenServico"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = ServicoAutenticado.Header,
                },
            };
            foreach (var caminho in documento.Paths.Where(caminho => caminho.Key.StartsWith("/render/")))
            {
                foreach (var operacao in caminho.Value.Operations!.Values)
                {
                    operacao.Security = [new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("tokenServico", documento)] = [],
                    }];
                    foreach (var conteudo in operacao.Responses!["200"].Content!.Values)
                        conteudo.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
                    if (caminho.Key == "/render/pdf")
                    {
                        var resposta = (OpenApiResponse)operacao.Responses["200"];
                        resposta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                        resposta.Headers["X-Paginas"] = new OpenApiHeader
                        {
                            Description = "Numero de paginas medido pelo QuestPDF",
                            Required = true,
                            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1" },
                        };
                    }
                }
            }
            return Task.CompletedTask;
        });
    }
}
