using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Helpers;

public sealed class IdempotentCommandExecutor : IIdempotentCommandExecutor
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ICommandExecutionService _commands;

    public IdempotentCommandExecutor(ICommandExecutionService commands)
    {
        _commands = commands;
    }

    public async Task<IResult> ExecuteAsync<T>(
        HttpContext ctx,
        int userId,
        string operation,
        string? idempotencyKey,
        Func<CancellationToken, Task<T>> action,
        Func<T, IResult> resultMapper,
        ILogger log,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var value = await action(ct);
            return resultMapper(value);
        }

        var acquired = await _commands.AcquireAsync(userId, operation, idempotencyKey, ct);
        using var _ = RequestContextHelper.PushOperationContext(ctx, operation, userId, idempotencyKey);

        if (!acquired.ShouldExecute)
        {
            log.LogWarning(
                "Command replay detected. Operation={Operation} CommandStatus={CommandStatus} RetryAttempt={RetryAttempt}",
                operation, acquired.Execution.Status, acquired.Execution.RetryAttempt);
            if (!string.IsNullOrWhiteSpace(acquired.Execution.ResponseBody))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<T>(acquired.Execution.ResponseBody, JsonOpts);
                    if (cached is not null)
                        return resultMapper(cached);
                    log.LogWarning(
                        "Idempotency replay deserialize returned null. Operation={Operation} ExecutionId={ExecutionId}",
                        operation, acquired.Execution.Id);
                }
                catch (JsonException ex)
                {
                    log.LogError(ex,
                        "Idempotency replay deserialize failed; falling back to generic 200 envelope. Operation={Operation} ExecutionId={ExecutionId}",
                        operation, acquired.Execution.Id);
                }
            }
            return Results.Ok(new
            {
                message = "Command already processed.",
                status = acquired.Execution.Status.ToString()
            });
        }

        try
        {
            var value = await action(ct);
            await _commands.CompleteAsync(acquired.Execution.Id, JsonSerializer.Serialize(value, JsonOpts), ct);
            log.LogInformation(
                "Command completed. Operation={Operation} CommandStatus={CommandStatus} RetryAttempt={RetryAttempt}",
                operation, CommandExecutionStatus.Completed, acquired.Execution.RetryAttempt + 1);
            return resultMapper(value);
        }
        catch (InvalidOperationException)
        {
            await _commands.FailAsync(acquired.Execution.Id, retriable: false, ct);
            throw;
        }
        catch
        {
            await _commands.FailAsync(acquired.Execution.Id, retriable: true, ct);
            throw;
        }
    }
}
