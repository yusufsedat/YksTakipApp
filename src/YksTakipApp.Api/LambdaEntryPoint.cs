using Amazon.Lambda.AspNetCoreServer;

namespace YksTakipApp.Api;

/// <summary>
/// AWS Lambda için entry point
/// Minimal API için APIGatewayHttpApiV2ProxyFunction kullanıyor
/// </summary>
public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseLambdaServer();
    }
}

