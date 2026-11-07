using System.Threading.RateLimiting;

namespace Laminar.Tests.Helpers;

internal sealed class TracingRateLimiter : RateLimiter
{
    private readonly RateLimiter inner;
    private readonly List<string> log;

    internal TracingRateLimiter(RateLimiter inner, List<string> log)
    {
        this.inner = inner;
        this.log = log;
    }

    public override TimeSpan? IdleDuration => inner.IdleDuration;
    public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
        => Wrap(inner.AttemptAcquire(permitCount));

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permits, CancellationToken token)
        => Wrap(await inner.AcquireAsync(permits, token).ConfigureAwait(false));

    private Lease Wrap(RateLimitLease lease)
    {
        log.Add(lease.IsAcquired ? "acquire" : "reject");
        return new Lease(lease, log);
    }

    private sealed class Lease : RateLimitLease
    {
        private readonly RateLimitLease inner;
        private readonly List<string> log;
        private bool disposed;

        internal Lease(RateLimitLease inner, List<string> log)
        {
            this.inner = inner;
            this.log = log;
        }

        public override bool IsAcquired => inner.IsAcquired;
        public override IEnumerable<string> MetadataNames => inner.MetadataNames;
        public override bool TryGetMetadata(string name, out object? metadata)
            => inner.TryGetMetadata(name, out metadata);

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                inner.Dispose();
                log.Add("release");
            }
        }
    }
}
