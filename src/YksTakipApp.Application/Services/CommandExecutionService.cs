using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class CommandExecutionService : ICommandExecutionService
{
    private readonly AppDbContext _db;

    public CommandExecutionService(AppDbContext db) => _db = db;

    public async Task<CommandExecutionAcquireResult> AcquireAsync(int userId, string operation, string commandKey, CancellationToken ct)
    {
        var existing = await _db.CommandExecutions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Operation == operation && x.CommandKey == commandKey, ct);
        if (existing is not null)
        {
            var shouldExecute = existing.Status is CommandExecutionStatus.FailedRetriable;
            return new CommandExecutionAcquireResult(shouldExecute, existing, IsReplay: !shouldExecute);
        }

        var created = new CommandExecution
        {
            UserId = userId,
            Operation = operation,
            CommandKey = commandKey,
            Status = CommandExecutionStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };
        _db.CommandExecutions.Add(created);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new CommandExecutionAcquireResult(true, created, IsReplay: false);
        }
        catch (DbUpdateException)
        {
            var collision = await _db.CommandExecutions
                .FirstAsync(x => x.UserId == userId && x.Operation == operation && x.CommandKey == commandKey, ct);
            var shouldExecute = collision.Status is CommandExecutionStatus.FailedRetriable;
            return new CommandExecutionAcquireResult(shouldExecute, collision, IsReplay: !shouldExecute);
        }
    }

    public async Task<CommandExecution> CompleteAsync(long executionId, string responseBody, CancellationToken ct)
    {
        var row = await _db.CommandExecutions.FirstAsync(x => x.Id == executionId, ct);
        row.Status = CommandExecutionStatus.Completed;
        row.ResponseBody = responseBody;
        row.ResponseHash = Sha256(responseBody);
        row.CompletedAt = DateTime.UtcNow;
        row.RetryAttempt += 1;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<CommandExecution> FailAsync(long executionId, bool retriable, CancellationToken ct)
    {
        var row = await _db.CommandExecutions.FirstAsync(x => x.Id == executionId, ct);
        row.Status = retriable ? CommandExecutionStatus.FailedRetriable : CommandExecutionStatus.FailedNonRetriable;
        row.CompletedAt = DateTime.UtcNow;
        row.RetryAttempt += 1;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private static string Sha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
