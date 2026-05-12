using Serilog.Context;

namespace YksTakipApp.Api.Helpers;

public static class RequestContextHelper
{
    public static string? ResolveIdempotencyKey(HttpContext ctx, string? bodyKey)
    {
        var headerKey = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var key = string.IsNullOrWhiteSpace(headerKey) ? bodyKey : headerKey;
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return key.Trim();
    }

    public static IDisposable PushOperationContext(
        HttpContext ctx,
        string operationName,
        int? userId,
        string? idempotencyKey)
    {
        var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
        return new CompositeDisposable(
            LogContext.PushProperty("OperationName", operationName),
            LogContext.PushProperty("Endpoint", ctx.Request.Path.Value ?? operationName),
            LogContext.PushProperty("CorrelationId", correlationId),
            LogContext.PushProperty("RequestId", ctx.TraceIdentifier),
            LogContext.PushProperty("UserId", userId),
            LogContext.PushProperty("IdempotencyKey", idempotencyKey));
    }

    private sealed class CompositeDisposable(params IDisposable[] items) : IDisposable
    {
        private readonly IDisposable[] _items = items;

        public void Dispose()
        {
            for (var i = _items.Length - 1; i >= 0; i--)
                _items[i].Dispose();
        }
    }
}
