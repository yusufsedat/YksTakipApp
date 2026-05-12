namespace YksTakipApp.Core.Models;

public sealed record IdempotentCreateResult<T>(T Entity, bool IsReplay);
