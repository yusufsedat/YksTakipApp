using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

namespace YksTakipApp.Api.Helpers
{
    public static class SecretsLoader
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static IAmazonSecretsManager? _cachedClient;

        private static IAmazonSecretsManager GetClient()
        {
            // Lambda veya ECS task ortamında AWS credentials otomatik gelir
            return _cachedClient ??= new AmazonSecretsManagerClient();
        }

        public static async Task<IDictionary<string, string>?> TryLoadFromAwsAsync(
            string? secretName, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(secretName))
                return null;

            try
            {
                var client = GetClient();

                var response = await client.GetSecretValueAsync(
                    new GetSecretValueRequest { SecretId = secretName },
                    cancellationToken
                );

                string? jsonPayload = null;

                if (!string.IsNullOrWhiteSpace(response.SecretString))
                {
                    jsonPayload = response.SecretString;
                }
                else if (response.SecretBinary != null)
                {
                    jsonPayload = System.Text.Encoding.UTF8.GetString(response.SecretBinary.ToArray());
                }

                if (string.IsNullOrWhiteSpace(jsonPayload))
                    return null;

                if (JsonSerializer.Deserialize<Dictionary<string, string>>(jsonPayload, _jsonOptions)
                    is Dictionary<string, string> dict)
                {
                    return dict;
                }

                return null;
            }
            catch
            {
                // AWS SDK bulunamazsa veya erişim hatası varsa sessizce devam et
                // Lambda ortamında AWS credentials otomatik olarak sağlanır
                // Hata detayları için logger eklenebilir
                return null;
            }
        }
    }
}
