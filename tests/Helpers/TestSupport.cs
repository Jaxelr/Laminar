using System.Threading.RateLimiting;

namespace Laminar.Tests.Helpers;

internal static class TestSupport
{
    internal static readonly TimeSpan Safety = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan Period = TimeSpan.FromSeconds(10);
    internal static readonly object Sentinel = new();

    internal static Task Guard(this Task task) => task.WaitAsync(Safety);
    internal static Task<T> Guard<T>(this Task<T> task) => task.WaitAsync(Safety);
    internal static Task Guard(this ValueTask task) => task.AsTask().WaitAsync(Safety);
    internal static Task<T> Guard<T>(this ValueTask<T> task) => task.AsTask().WaitAsync(Safety);

    internal static ConcurrencyLimiter Concurrency(int permits = 1, int queue = 0)
        => new(new ConcurrencyLimiterOptions
        {
            PermitLimit = permits,
            QueueLimit = queue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

    internal static TokenBucketRateLimiter Bucket(TimeSpan? period = null)
        => new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 2,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = period ?? Period,
            AutoReplenishment = false,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

    internal static Func<CancellationToken, ValueTask<object>> Success(Action? callback = null)
        => _ =>
        {
            callback?.Invoke();
            return ValueTask.FromResult(Sentinel);
        };

    internal static Func<CancellationToken, ValueTask<object>> Failure(Exception exception)
        => _ => throw exception;

    internal static CircuitBreakerResilience Breaker(
        RecordingTimeProvider clock, int threshold = 2, Func<Exception, bool>? predicate = null)
        => new(new CircuitBreakerOptions
        {
            FailureThreshold = threshold,
            BreakDuration = Period,
            TimeProvider = clock,
            ShouldHandle = predicate ?? new CircuitBreakerOptions().ShouldHandle,
        });

    internal static async Task Fail(CircuitBreakerResilience breaker, int count = 2)
    {
        var exception = new InvalidOperationException("handled");
        for (int i = 0; i < count; i++)
        {
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await breaker.Apply(Failure(exception))(default).Guard());
            Assert.Same(exception, actual);
        }
    }

    internal static async Task Rejected(CircuitBreakerResilience breaker)
    {
        int calls = 0;
        await Assert.ThrowsAsync<CircuitOpenException>(
            async () => await breaker.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(0, calls);
    }
}
