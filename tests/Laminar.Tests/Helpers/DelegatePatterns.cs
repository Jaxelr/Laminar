using Microsoft.Extensions.Time.Testing;

namespace Laminar.Tests.Helpers;

internal sealed class DelegatePatterns : IDisposable
{
    internal ControlledRateLimiter Limiter { get; }
    internal FakeTimeProvider Clock { get; } = new();
    internal RecordingResilience Custom { get; } = new("custom", []);
    internal CircuitBreakerResilience Breaker { get; }
    private readonly RetryOptions retry = new() { MaxRetries = 1 };

    internal DelegatePatterns(bool permitsAvailable = true)
    {
        Limiter = new() { Acquired = permitsAvailable };
        Breaker = new(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            TimeProvider = Clock
        });
    }

    internal Func<CancellationToken, ValueTask<object>> Wrap(
        DelegatePatternTests.Shape shape,
        DelegatePatternTests.Pattern pattern,
        Func<CancellationToken, ValueTask<object>> callback)
        => shape switch
        {
            DelegatePatternTests.Shape.Func => Apply((Func<object>) (() => InvokeSynchronously(callback)), pattern),
            DelegatePatternTests.Shape.Action => WithResult(Apply((Action) (() => _ = InvokeSynchronously(callback)), pattern)),
            DelegatePatternTests.Shape.TaskResult => Apply((Func<Task<object>>) (() => callback(default).AsTask()), pattern),
            DelegatePatternTests.Shape.Task => WithResult(Apply((Func<Task>) (() => callback(default).AsTask()), pattern)),
            DelegatePatternTests.Shape.TokenTaskResult => Apply((Func<CancellationToken, Task<object>>) (token => callback(token).AsTask()), pattern),
            DelegatePatternTests.Shape.TokenTask => WithResult(Apply((Func<CancellationToken, Task>) (token => callback(token).AsTask()), pattern)),
            DelegatePatternTests.Shape.ValueTaskResult => Apply((Func<ValueTask<object>>) (() => callback(default)), pattern),
            DelegatePatternTests.Shape.ValueTask => WithResult(Apply((Func<ValueTask>) (async () => _ = await callback(default)), pattern)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

    internal Func<CancellationToken, ValueTask<object>> WrapNull(
        DelegatePatternTests.Shape shape,
        DelegatePatternTests.Pattern pattern)
        => shape switch
        {
            DelegatePatternTests.Shape.Func => Apply((Func<object>) null!, pattern),
            DelegatePatternTests.Shape.Action => WithResult(Apply((Action) null!, pattern)),
            DelegatePatternTests.Shape.TaskResult => Apply((Func<Task<object>>) null!, pattern),
            DelegatePatternTests.Shape.Task => WithResult(Apply((Func<Task>) null!, pattern)),
            DelegatePatternTests.Shape.TokenTaskResult => Apply((Func<CancellationToken, Task<object>>) null!, pattern),
            DelegatePatternTests.Shape.TokenTask => WithResult(Apply((Func<CancellationToken, Task>) null!, pattern)),
            DelegatePatternTests.Shape.ValueTaskResult => Apply((Func<ValueTask<object>>) null!, pattern),
            DelegatePatternTests.Shape.ValueTask => WithResult(Apply((Func<ValueTask>) null!, pattern)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

    private Func<CancellationToken, ValueTask<object>> Apply(
        Func<object> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask> Apply(
        Action operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask<object>> Apply(
        Func<Task<object>> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask> Apply(
        Func<Task> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask<object>> Apply(
        Func<CancellationToken, Task<object>> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask> Apply(
        Func<CancellationToken, Task> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask<object>> Apply(
        Func<ValueTask<object>> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private Func<CancellationToken, ValueTask> Apply(
        Func<ValueTask> operation, DelegatePatternTests.Pattern pattern)
        => pattern switch
        {
            DelegatePatternTests.Pattern.RateLimit => operation.RateLimitedBy(Limiter, permitCount: 2),
            DelegatePatternTests.Pattern.Retry => operation.Retryable(retry),
            DelegatePatternTests.Pattern.Timeout => operation.TimedOutAfter(Period, Clock),
            DelegatePatternTests.Pattern.CircuitBreaker => operation.CircuitBrokenBy(Breaker),
            DelegatePatternTests.Pattern.Custom => operation.ResilientWith(Custom),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private static Func<CancellationToken, ValueTask<object>> WithResult(
        Func<CancellationToken, ValueTask> operation)
        => async token =>
        {
            await operation(token);
            return Sentinel;
        };

    private static object InvokeSynchronously(Func<CancellationToken, ValueTask<object>> callback)
    {
        var result = callback(default);
        if (!result.IsCompleted)
        {
            throw new InvalidOperationException("Synchronous test delegates require a completed callback.");
        }

        return result.GetAwaiter().GetResult();
    }

    public void Dispose() => Limiter.Dispose();
}
