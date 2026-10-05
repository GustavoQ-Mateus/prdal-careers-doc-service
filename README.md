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

## Lambda

`Dockerfile.lambda` usa a imagem oficial `public.ecr.aws/lambda/dotnet:10`.
O handler é `doc-service::DocService.Lambda::FunctionHandlerAsync`, com eventos
HTTP API v2 do API Gateway ou Function URL. PDF e DOCX voltam em Base64,
com `isBase64Encoded: true`; o PDF conserva `X-Paginas`.
O mesmo pipeline de autenticação, corpo e prazo atende HTTP e Lambda.
O processo não usa banco nem mantém dados de candidato entre invocações.

A escolha é container com SnapStart. A documentação atual da AWS já
suporta esse formato para .NET 8 e posteriores, diferente da restrição
que motivou a alternativa ZIP no prompt:
https://docs.aws.amazon.com/lambda/latest/dg/snapstart.html
https://aws.amazon.com/blogs/compute/net-10-runtime-now-available-in-aws-lambda/

A infraestrutura deve publicar uma versão, habilitar SnapStart nessa versão,
apontar o alias, limitar a concorrência e manter a função fora da VPC.
Configure memória inicial de 1024 MB, timeout da função de 20 segundos,
armazenamento temporário de 512 MB e o limite interno de render de 15 segundos.
Nenhum recurso AWS é criado por este repositório.

Estimativa de partida a frio: 1 a 5 segundos sem snapshot; abaixo de 1 segundo
com SnapStart em condições favoráveis, conforme a AWS. São estimativas,
não medições desta função na nuvem. O primeiro carregamento nativo e render
podem acrescentar latência e devem ser medidos pela infraestrutura.
