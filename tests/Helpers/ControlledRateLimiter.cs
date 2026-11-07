using System.Threading.RateLimiting;

namespace Laminar.Tests.Helpers;

internal sealed class ControlledRateLimiter : RateLimiter
{
    internal Action? OnAcquire { get; init; }
    internal bool Acquired { get; init; } = true;
    internal TimeSpan? RetryAfter { get; init; }
    internal int Acquisitions { get; private set; }
    internal int Disposals { get; private set; }
    internal int Permits { get; private set; }
    public override TimeSpan? IdleDuration => null;
    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Acquire(permitCount);
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken token)
        => ValueTask.FromResult<RateLimitLease>(Acquire(permitCount));

    private Lease Acquire(int permits)
    {
        Acquisitions++;
        Permits = permits;
        OnAcquire?.Invoke();
        return new Lease(this);
    }

    private sealed class Lease : RateLimitLease
    {
        private readonly ControlledRateLimiter owner;
        private bool disposed;

        internal Lease(ControlledRateLimiter owner)
        {
            this.owner = owner;
        }

        public override bool IsAcquired => owner.Acquired;
        public override IEnumerable<string> MetadataNames
            => owner.RetryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

        public override bool TryGetMetadata(string name, out object? metadata)
        {
            metadata = owner.RetryAfter;
            return name == MetadataName.RetryAfter.Name && owner.RetryAfter.HasValue;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                owner.Disposals++;
            }
        }
    }
}
