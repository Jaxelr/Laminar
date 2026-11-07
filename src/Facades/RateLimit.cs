using System.Threading.RateLimiting;

namespace Laminar;

public static class RateLimit
{
    public static ValueTask RateLimitedBy(
        Func<Task> operation,
        RateLimiter limiter,
        int permitCount = 1,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.RateLimitedBy(operation, limiter, permitCount)(cancellationToken);

    public static ValueTask<TResult> RateLimitedBy<TResult>(
        Func<Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.RateLimitedBy(operation, limiter, permitCount)(cancellationToken);

    public static ValueTask RateLimitedBy(
        Func<CancellationToken, Task> operation,
        RateLimiter limiter,
        int permitCount = 1,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.RateLimitedBy(operation, limiter, permitCount)(cancellationToken);

    public static ValueTask<TResult> RateLimitedBy<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.RateLimitedBy(operation, limiter, permitCount)(cancellationToken);
}
