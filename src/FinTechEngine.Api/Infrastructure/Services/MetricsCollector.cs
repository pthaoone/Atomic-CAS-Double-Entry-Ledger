using System.Collections.Concurrent;
using FinTechEngine.Api.Models;

namespace FinTechEngine.Api.Infrastructure.Services;

public interface IMetricsCollector
{
    void RecordRequest();
    void RecordSuccess(long latencyMs, int retries);
    void RecordFailure(long latencyMs);
    void RecordConflict();
    void RecordRetry();
    EngineMetricsDto GetSnapshot();
    void Reset();
}

public class MetricsCollector : IMetricsCollector
{
    private long _totalRequests;
    private long _successfulTransactions;
    private long _failedTransactions;
    private long _casConflictCount;
    private long _totalRetries;
    private long _totalLatencyMs;
    private readonly ConcurrentQueue<DateTime> _recentSuccessTimestamps = new();

    public void RecordRequest()
    {
        Interlocked.Increment(ref _totalRequests);
    }

    public void RecordSuccess(long latencyMs, int retries)
    {
        Interlocked.Increment(ref _successfulTransactions);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
        if (retries > 0)
        {
            Interlocked.Add(ref _totalRetries, retries);
        }

        var now = DateTime.UtcNow;
        _recentSuccessTimestamps.Enqueue(now);

        // Prune entries older than 5 seconds
        while (_recentSuccessTimestamps.TryPeek(out var oldest) && (now - oldest).TotalSeconds > 5)
        {
            _recentSuccessTimestamps.TryDequeue(out _);
        }
    }

    public void RecordFailure(long latencyMs)
    {
        Interlocked.Increment(ref _failedTransactions);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
    }

    public void RecordConflict()
    {
        Interlocked.Increment(ref _casConflictCount);
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _totalRetries);
    }

    public EngineMetricsDto GetSnapshot()
    {
        var totalReq = Interlocked.Read(ref _totalRequests);
        var success = Interlocked.Read(ref _successfulTransactions);
        var failed = Interlocked.Read(ref _failedTransactions);
        var conflicts = Interlocked.Read(ref _casConflictCount);
        var retries = Interlocked.Read(ref _totalRetries);
        var latencySum = Interlocked.Read(ref _totalLatencyMs);

        var totalProcessed = success + failed;
        var avgLatency = totalProcessed > 0 ? (double)latencySum / totalProcessed : 0;

        // Calculate TPS based on window of last 5 seconds
        var now = DateTime.UtcNow;
        var recentCount = _recentSuccessTimestamps.Count(t => (now - t).TotalSeconds <= 1.0);
        var tps = recentCount; // transactions in the last second

        return new EngineMetricsDto(
            TotalRequests: totalReq,
            SuccessfulTransactions: success,
            FailedTransactions: failed,
            CasConflictCount: conflicts,
            TotalRetries: retries,
            AverageLatencyMs: Math.Round(avgLatency, 2),
            CurrentTps: tps,
            ActiveConnections: 1
        );
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _successfulTransactions, 0);
        Interlocked.Exchange(ref _failedTransactions, 0);
        Interlocked.Exchange(ref _casConflictCount, 0);
        Interlocked.Exchange(ref _totalRetries, 0);
        Interlocked.Exchange(ref _totalLatencyMs, 0);
        while (_recentSuccessTimestamps.TryDequeue(out _)) { }
    }
}
