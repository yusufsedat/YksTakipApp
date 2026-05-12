using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace YksTakipApp.Api.Helpers;

/// <summary>
/// Idempotency anahtarı verildiğinde komutu tek sefer çalıştırıp aynı yanıtı tekrar dönen yürütücü.
/// HTTP status kodu <paramref name="resultMapper"/> üzerinden belirlenir; replay sırasında önbellekli
/// yanıt deserialize edilip aynı mapper'a gönderilir, böylece custom status code (ör. 422) korunur.
/// </summary>
public interface IIdempotentCommandExecutor
{
    Task<IResult> ExecuteAsync<T>(
        HttpContext ctx,
        int userId,
        string operation,
        string? idempotencyKey,
        Func<CancellationToken, Task<T>> action,
        Func<T, IResult> resultMapper,
        ILogger log,
        CancellationToken ct);
}
