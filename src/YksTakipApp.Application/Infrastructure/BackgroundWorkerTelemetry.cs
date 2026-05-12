using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Infrastructure;

public sealed class BackgroundWorkerTelemetry : IBackgroundWorkerTelemetry
{
    private volatile bool _isRunning;
    private DateTimeOffset? _lastHeartbeatUtc;
    private DateTimeOffset? _lastFailureUtc;
    private string? _lastError;

    public bool IsRunning => _isRunning;
    public DateTimeOffset? LastHeartbeatUtc => _lastHeartbeatUtc;
    public DateTimeOffset? LastFailureUtc => _lastFailureUtc;
    public string? LastError => _lastError;

    public void MarkStarted()
    {
        _isRunning = true;
        _lastError = null;
        _lastFailureUtc = null;
        _lastHeartbeatUtc = DateTimeOffset.UtcNow;
    }

    public void MarkHeartbeat() => _lastHeartbeatUtc = DateTimeOffset.UtcNow;

    public void MarkFailure(Exception ex)
    {
        _lastFailureUtc = DateTimeOffset.UtcNow;
        _lastError = ex.Message;
    }

    public void MarkStopped() => _isRunning = false;
}
