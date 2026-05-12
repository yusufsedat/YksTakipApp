using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces;

public interface ICommandExecutionService
{
    Task<CommandExecutionAcquireResult> AcquireAsync(int userId, string operation, string commandKey, CancellationToken ct);
    Task<CommandExecution> CompleteAsync(long executionId, string responseBody, CancellationToken ct);
    Task<CommandExecution> FailAsync(long executionId, bool retriable, CancellationToken ct);
}

public sealed record CommandExecutionAcquireResult(bool ShouldExecute, CommandExecution Execution, bool IsReplay);
