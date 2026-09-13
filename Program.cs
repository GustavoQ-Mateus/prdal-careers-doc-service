var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.MapGet("/health", () => new HealthResponse("doc-service", "ok"));

app.MapGet("/hello", () =>
{
    var hop = new HelloHop("doc-service", "hello from doc-service");
    return new HelloResponse("doc-service", hop.Message, new[] { hop });
});

app.Run();

record HealthResponse(string Service, string Status);
record HelloHop(string Service, string Message);
record HelloResponse(string Service, string Message, HelloHop[] Chain);
