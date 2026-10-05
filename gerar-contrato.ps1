$ErrorActionPreference = 'Stop'
$anterior = $env:GERAR_CONTRATO
try {
    $env:GERAR_CONTRATO = '1'
    dotnet test (Join-Path $PSScriptRoot 'tests/DocService.Tests.csproj') --filter FullyQualifiedName~ContratoGeradoCorrespondeAoArquivo
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar contrato' }
}
finally {
    $env:GERAR_CONTRATO = $anterior
}
