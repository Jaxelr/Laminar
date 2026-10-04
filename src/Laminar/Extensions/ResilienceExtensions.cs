using System.Threading.RateLimiting;

namespace Laminar;

/// <summary>
/// Decorates delegates with rate limiting, retry, cooperative timeout, and shared circuit breaking.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Rate-limits a synchronous result delegate, acquiring permits before each invocation.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<TResult> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a synchronous action, acquiring permits before each invocation.
    /// </summary>
    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Action operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a task-returning result delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a task-returning delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<Task> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a cancellation-aware task-returning result delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a cancellation-aware task-returning delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<CancellationToken, Task> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a value-task-returning result delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<ValueTask<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a value-task-returning delegate, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<ValueTask> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.AsOperation().RateLimitedBy(limiter, permitCount);

    /// <summary>
    /// Rate-limits a cancellation-aware result operation, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> RateLimitedBy<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.ResilientWith(new RateLimitResilience(limiter, permitCount));

    /// <summary>
    /// Rate-limits a cancellation-aware operation, holding its lease until completion.
    /// </summary>
    public static Func<CancellationToken, ValueTask> RateLimitedBy(
        this Func<CancellationToken, ValueTask> operation,
        RateLimiter limiter,
        int permitCount = 1)
        => operation.ResilientWith(new RateLimitResilience(limiter, permitCount));

    /// <summary>
    /// Retries handled failures of a cancellation-aware result operation, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        RetryOptions? options = null)
        => operation.ResilientWith(new RetryResilience(options ?? new RetryOptions()));

    /// <summary>
    /// Retries handled failures of a synchronous result delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<TResult> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a synchronous action, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask> Retryable(
        this Action operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a task-returning result delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<Task<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a task-returning delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<Task> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a cancellation-aware task-returning result delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a cancellation-aware task-returning delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<CancellationToken, Task> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a value-task-returning result delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> Retryable<TResult>(
        this Func<ValueTask<TResult>> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a value-task-returning delegate, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<ValueTask> operation,
        RetryOptions? options = null)
        => operation.AsOperation().Retryable(options);

    /// <summary>
    /// Retries handled failures of a cancellation-aware operation, using default options when none are supplied.
    /// </summary>
    public static Func<CancellationToken, ValueTask> Retryable(
        this Func<CancellationToken, ValueTask> operation,
        RetryOptions? options = null)
        => operation.ResilientWith(new RetryResilience(options ?? new RetryOptions()));

    /// <summary>
    /// Applies a cooperative deadline to a cancellation-aware result operation without abandoning execution.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    /// <summary>
    /// Rejects a synchronous delegate's late result after inline execution; the delegate cannot observe deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<TResult> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Rejects a synchronous action's late completion after inline execution; the action cannot observe deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Action operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Rejects a task-returning delegate's late result without abandoning execution or forwarding deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Rejects a task-returning delegate's late completion without abandoning execution or forwarding deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Applies a cooperative deadline to a cancellation-aware task-returning result delegate without abandoning execution.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Applies a cooperative deadline to a cancellation-aware task-returning delegate without abandoning execution.
    /// </summary>
    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Rejects a value-task-returning delegate's late result without abandoning execution or forwarding deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> TimedOutAfter<TResult>(
        this Func<ValueTask<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Rejects a value-task-returning delegate's late completion without abandoning execution or forwarding deadline cancellation.
    /// </summary>
    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<ValueTask> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.AsOperation().TimedOutAfter(timeout, timeProvider);

    /// <summary>
    /// Applies a cooperative deadline to a cancellation-aware operation without abandoning execution.
    /// </summary>
    public static Func<CancellationToken, ValueTask> TimedOutAfter(
        this Func<CancellationToken, ValueTask> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
        => operation.ResilientWith(new TimeoutResilience(timeout, timeProvider));

    /// <summary>
    /// Guards a cancellation-aware result operation with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);

    /// <summary>
    /// Guards a synchronous result delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<TResult> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a synchronous action with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Action operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a task-returning result delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a task-returning delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<Task> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a cancellation-aware task-returning result delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a cancellation-aware task-returning delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<CancellationToken, Task> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a value-task-returning result delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> CircuitBrokenBy<TResult>(
        this Func<ValueTask<TResult>> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a value-task-returning delegate with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<ValueTask> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.AsOperation().CircuitBrokenBy(circuitBreaker);

    /// <summary>
    /// Guards a cancellation-aware operation with the supplied breaker's shared state.
    /// </summary>
    public static Func<CancellationToken, ValueTask> CircuitBrokenBy(
        this Func<CancellationToken, ValueTask> operation,
        CircuitBreakerResilience circuitBreaker)
        => operation.ResilientWith(circuitBreaker);
}
