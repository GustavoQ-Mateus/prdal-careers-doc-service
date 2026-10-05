using DocService;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");
Inicializacao.ConfigurarServicos(builder.Services);
var app = builder.Build();
Inicializacao.Configurar(app, app.Configuration);
app.Run();

public partial class Program;
