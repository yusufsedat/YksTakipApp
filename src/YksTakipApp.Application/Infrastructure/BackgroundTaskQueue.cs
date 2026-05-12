using System.Threading.Channels;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Infrastructure;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private int _pendingCount;
    private readonly Channel<AdaptationEvaluationJob> _queue =
        Channel.CreateUnbounded<AdaptationEvaluationJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public async ValueTask EnqueueAdaptationEvaluationAsync(AdaptationEvaluationJob job, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(job, ct);
        Interlocked.Increment(ref _pendingCount);
    }

    public async ValueTask<AdaptationEvaluationJob> DequeueAdaptationEvaluationAsync(CancellationToken ct)
    {
        var item = await _queue.Reader.ReadAsync(ct);
        Interlocked.Decrement(ref _pendingCount);
        return item;
    }
}
