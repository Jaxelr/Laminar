using System.Threading.RateLimiting;

namespace Laminar;

/// <summary>
/// Acquires rate-limiter permits and holds the lease until the operation completes.
/// </summary>
public sealed class RateLimitResilience : IOperationResilience
{
    private readonly RateLimiter limiter;
    private readonly int permitCount;

    /// <summary>
    /// Initializes rate limiting with a caller-owned limiter and a permit count that defaults to one.
    /// </summary>
    public RateLimitResilience(RateLimiter limiter, int permitCount = 1)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitCount);
        this.limiter = limiter;
        this.permitCount = permitCount;
    }

    /// <summary>
    /// Returns an operation that acquires permits before execution and throws on rate-limit rejection.
    /// </summary>
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
