using System.Threading.RateLimiting;

namespace Laminar;

public sealed class RateLimitResilience : IOperationResilience
{
    private readonly RateLimiter limiter;
    private readonly int permitCount;

    public RateLimitResilience(RateLimiter limiter, int permitCount = 1)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitCount);
        this.limiter = limiter;
        this.permitCount = permitCount;
    }

    public Func<CancellationToken, ValueTask<TResult>> Apply<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken => ExecuteAsync(operation, cancellationToken);
    }

    private async ValueTask<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = await limiter.AcquireAsync(permitCount, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            TimeSpan? retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                ? delay : null;
            throw new RateLimitRejectedException(retryAfter);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await operation(cancellationToken).ConfigureAwait(false);
    }
}
