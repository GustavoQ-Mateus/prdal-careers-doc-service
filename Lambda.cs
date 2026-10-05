using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace DocService;

public class Lambda : APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        RegisterResponseContentEncodingForContentType(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ResponseContentEncoding.Base64);
        builder.UseStartup<Inicializacao>();
    }
}
