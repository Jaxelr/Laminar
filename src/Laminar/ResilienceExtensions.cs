using System.Threading.RateLimiting;

namespace Laminar;

public static class ResilienceExtensions
{
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.ResilientWith(new RateLimitResilience(limiter, permitCount));

    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<CancellationToken, ValueTask> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.ResilientWith(new RateLimitResilience(limiter, permitCount));

    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        RetryOptions? options = null)
        => operation.ResilientWith(new RetryResilience(options ?? new RetryOptions()));

    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<CancellationToken, ValueTask> operation,
        RetryOptions? options = null)
        => operation.ResilientWith(new RetryResilience(options ?? new RetryOptions()));

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<CancellationToken, ValueTask> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<CancellationToken, ValueTask> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);
}
