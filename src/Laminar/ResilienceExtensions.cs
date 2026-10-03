using System.Threading.RateLimiting;

namespace Laminar;

public static class ResilienceExtensions
{
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<TResult> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Action operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<Task> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<CancellationToken, Task> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<ValueTask<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<ValueTask> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

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

    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<TResult> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask> Retryable(
        this Action operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<Task<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<Task> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<CancellationToken, Task> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<ValueTask<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<ValueTask> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<CancellationToken, ValueTask> operation,
        RetryOptions? options = null)
        => operation.ResilientWith(new RetryResilience(options ?? new RetryOptions()));

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<TResult> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Action operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<ValueTask<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<ValueTask> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<CancellationToken, ValueTask> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<TResult> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Action operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<Task> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<CancellationToken, Task> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<ValueTask<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<ValueTask> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<CancellationToken, ValueTask> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);
}
