using System.Threading.RateLimiting;

namespace Laminar.Tests.Helpers;

internal sealed class FakeReplenishingLimiter : RateLimiter
{
    private readonly RecordingTimeProvider clock;
    private long replenished;
    private int tokens = 2;

    internal FakeReplenishingLimiter(RecordingTimeProvider clock)
    {
        this.clock = clock;
        replenished = clock.GetTimestamp();
    }

    public override TimeSpan? IdleDuration => null;
    public override RateLimiterStatistics? GetStatistics() => null;

    internal bool TryReplenish()
    {
        if (clock.GetElapsedTime(replenished) < Period)
        {
            return false;
        }
        tokens = 2;
        replenished = clock.GetTimestamp();
        return true;
    }

    protected override RateLimitLease AttemptAcquireCore(int permits)
    {
        bool acquired = tokens >= permits;
        if (acquired)
        {
            tokens -= permits;
        }
        return new Lease(acquired, Period - clock.GetElapsedTime(replenished));
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permits, CancellationToken token)
        => ValueTask.FromResult(AttemptAcquireCore(permits));

    private sealed class Lease : RateLimitLease
    {
        private readonly bool acquired;
        private readonly TimeSpan remaining;

        internal Lease(bool acquired, TimeSpan remaining)
        {
            this.acquired = acquired;
            this.remaining = remaining;
        }

        public override bool IsAcquired => acquired;
        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];
        public override bool TryGetMetadata(string name, out object? metadata)
        {
            metadata = remaining;
            return !acquired && name == MetadataName.RetryAfter.Name;
        }
    }
}
