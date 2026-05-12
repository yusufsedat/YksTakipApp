namespace YksTakipApp.Core.Entities;

public enum CommandExecutionStatus
{
    InProgress = 0,
    Completed = 1,
    FailedRetriable = 2,
    FailedNonRetriable = 3
}

public sealed class CommandExecution
{
    public long Id { get; set; }
    public string CommandKey { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public int UserId { get; set; }
    public CommandExecutionStatus Status { get; set; } = CommandExecutionStatus.InProgress;
    public string? ResponseBody { get; set; }
    public string? ResponseHash { get; set; }
    public int RetryAttempt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
