# Doc-service

Implementa a spec v1.11.0 em .NET 10. As rotas POST `/render/pdf` e
`/render/docx` recebem `{ "markdown": "# Nome", "template": null }` e exigem
`X-Prdal-Servico` fora de desenvolvimento. `/health` continua público.

O PDF retorna `X-Paginas`, inteiro positivo calculado pelo contexto de
paginação do QuestPDF durante a própria renderização. O corpo continua
sendo o PDF binário. Consumidores antigos podem continuar lendo apenas o corpo.

Os limites padrão são 200 KB de Markdown em UTF-8 e 15 segundos por render.
`RENDER_MAX_MARKDOWN_BYTES` e `RENDER_TIMEOUT_MS` permitem configurá-los.
O prazo limita a espera pela resposta; a chamada nativa em andamento não
é interrompida. Na Lambda, o timeout da função encerra o processo se necessário.

Execute `dotnet test tests/DocService.Tests.csproj` para verificar o serviço.

## Lambda

`Dockerfile.lambda` usa a imagem oficial `public.ecr.aws/lambda/dotnet:10`.
Publique a imagem para `linux/amd64` e configure a função com arquitetura `x86_64`.
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

O log `INIT REPORT` do emulador mediu 14,168 segundos de inicialização.
A primeira invocação PDF levou 16,203 segundos, incluindo inicialização
e render; a segunda, com cinco páginas, levou 0,156 segundo.
Esses tempos foram medidos sob carga local e não medem a partida a frio pura
na AWS. A AWS estima startup abaixo de um segundo com SnapStart em condições
favoráveis. O primeiro carregamento nativo e render podem acrescentar latência
e devem ser medidos pela infraestrutura.

A invocação síncrona da Lambda admite eventos de até 6 MB. O emulador corta
eventos maiores antes do handler, por isso a prova na imagem usa Markdown
de 512 KB, acima dos 200 KB permitidos. Os testes .NET chamam diretamente
o handler com 8 MB, em PDF, DOCX e Base64, e verificam 413 antes do render:
https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-limits.html

## Contrato

`contrato/openapi.json` é gerado pelo OpenAPI nativo do ASP.NET Core 10,
em OpenAPI 3.0 para consumo pelo pacote de contratos. O teste compara o
documento completo gerado com o arquivo versionado e falha se divergir.
Execute `powershell -File gerar-contrato.ps1` e revise o diff para atualizá-lo.
A rota `/openapi/v1.json` existe apenas com `PRDAL_AMBIENTE=desenvolvimento`.
O teste de produção verifica que essa rota retorna 404 com credencial válida.

## Verificação das imagens

Execute na raiz do monorepo:

```powershell
docker build -f apps/doc-service/Dockerfile -t prdal-doc-codex-http apps/doc-service
docker build -f apps/doc-service/Dockerfile.lambda -t prdal-doc-codex-lambda apps/doc-service
docker compose -p doc-codex-c4a -f apps/doc-service/tests/compose.verificacao.yml up -d --wait --wait-timeout 60
python apps/doc-service/tests/verificar-imagens.py
docker compose -p doc-codex-c4a -f apps/doc-service/tests/compose.verificacao.yml down
```

O projeto isolado usa somente as portas locais 18084 e 18085, sem banco nem
volumes. A prova cobre PDF de uma e várias páginas, DOCX, recusa de 8 MB em
HTTP e recusa de 512 KB no emulador Lambda. O tempo impresso é de invocação
no emulador local, sem SnapStart.
