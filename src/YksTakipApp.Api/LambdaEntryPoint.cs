using Amazon.Lambda.AspNetCoreServer;

namespace YksTakipApp.Api;

/// <summary>
/// AWS Lambda için entry point. SAM template tarafından kullanılır.
/// Minimal API için APIGatewayHttpApiV2ProxyFunction kullanıyoruz.
/// </summary>
public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        // Program.cs'deki tüm konfigürasyonu kullan
        // AddAWSLambdaHosting zaten Program.cs'de eklenmiş
        builder
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseLambdaServer();
    }
}

