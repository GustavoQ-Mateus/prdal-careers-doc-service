# Doc-service

Implementa a spec v1.11.0 em .NET 10. As rotas POST `/render/pdf` e
`/render/docx` recebem `{ "markdown": "# Nome", "template": null }` e exigem
`X-Prdal-Servico` fora de desenvolvimento. `/health` continua público.

O PDF retorna `X-Paginas`, inteiro positivo calculado pelo contexto de
paginação do QuestPDF durante a própria renderização. O corpo continua
sendo o PDF binário. Consumidores antigos podem continuar lendo apenas o corpo.

Os limites padrão são 200 KB de Markdown em UTF-8 e 15 segundos por render.
`RENDER_MAX_MARKDOWN_BYTES` e `RENDER_TIMEOUT_MS` permitem configurá-los.

Execute `dotnet test tests/DocService.Tests.csproj` para verificar o serviço.
