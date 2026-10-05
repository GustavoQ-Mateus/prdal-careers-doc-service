using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var raiz = Directory.GetCurrentDirectory();
var processo = new ProcessStartInfo("git")
{
    WorkingDirectory = raiz,
    RedirectStandardOutput = true,
    UseShellExecute = false,
    StandardOutputEncoding = Encoding.UTF8
};
foreach (var argumento in new[] { "ls-files", "-z", "--", "." })
    processo.ArgumentList.Add(argumento);
using var git = Process.Start(processo) ?? throw new InvalidOperationException("git indisponivel");
var saida = git.StandardOutput.ReadToEnd();
git.WaitForExit();
if (git.ExitCode != 0)
    return git.ExitCode;
var arquivos = saida.Split('\0', StringSplitOptions.RemoveEmptyEntries);
var falhas = 0;
foreach (var arquivo in arquivos)
{
    var caminho = Path.Combine(raiz, arquivo);
    if (!File.Exists(caminho))
        continue;
    var texto = File.ReadAllText(caminho);
    var posicao = texto.IndexOf((char)0x2014);
    if (posicao >= 0)
    {
        Console.Error.WriteLine($"{arquivo}:{texto[..posicao].Count(c => c == '\n') + 1}: travessao proibido");
        falhas++;
    }
    if (!arquivo.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        continue;
    var arvore = CSharpSyntaxTree.ParseText(texto);
    foreach (var trivia in arvore.GetRoot().DescendantTrivia(descendIntoTrivia: true))
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            continue;
        var linha = arvore.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
        Console.Error.WriteLine($"{arquivo}:{linha}: comentario em codigo");
        falhas++;
    }
}
Console.WriteLine($"Texto verificado: {arquivos.Length} arquivos");
return falhas == 0 ? 0 : 1;
