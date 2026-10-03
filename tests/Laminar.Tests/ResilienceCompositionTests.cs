namespace Laminar.Tests;

public sealed class ResilienceCompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Composition_OrderChargesLimiterPerAttemptOrOncePerInvocation(bool isVoid, bool outerRetry)
    {
        // Arrange
        using var bucket = Bucket();
        var retry = new RetryOptions { MaxRetries = 1 };
        int calls = 0;
        Func<CancellationToken, ValueTask<object>> result = _ =>
        {
            if (++calls == 1)
            {
                throw new InvalidOperationException("first");
            }
            return ValueTask.FromResult(Sentinel);
        };
        Func<CancellationToken, ValueTask> action = _ =>
        {
            if (++calls == 1)
            {
                throw new InvalidOperationException("first");
            }
            return ValueTask.CompletedTask;
        };
        // Act
        if (isVoid)
        {
            var operation = outerRetry ? action.RateLimitedBy(bucket, 1).Retryable(retry)
                : action.Retryable(retry).RateLimitedBy(bucket, 1);
            await operation(default).Guard();
        }
        else
        {
            var operation = outerRetry ? result.RateLimitedBy(bucket, 1).Retryable(retry)
                : result.Retryable(retry).RateLimitedBy(bucket, 1);
            Assert.Same(Sentinel, await operation(default).Guard());
        }
        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(outerRetry ? 0 : 1, bucket.GetStatistics()!.CurrentAvailablePermits);
        // Arrange
        int remainingCalls = 0;
        var remaining = Success(() => remainingCalls++).RateLimitedBy(bucket, 1);
        // Act
        // Assert
        if (!outerRetry)
        {
            Assert.Same(Sentinel, await remaining(default).Guard());
        }
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await remaining(default).Guard());
        Assert.Equal(outerRetry ? 0 : 1, remainingCalls);
        Assert.Equal(0, bucket.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task VoidComposition_AllFourPoliciesAwaitFullCompletion()
    {
        // Arrange
        using var inner = Concurrency();
        var log = new List<string>();
        using var limiter = new TracingRateLimiter(inner, log);
        using var gate = new AsyncGate();
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        async ValueTask callback(CancellationToken _)
        {
            log.Add("callback");
            Assert.Equal(1, clock.Created);
            await gate.Wait().Guard();
        }
        var operation = AllFour(callback, limiter, clock, breaker);
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.Equal(Period, await clock.Registered());
        Assert.False(pending.IsCompleted);
        using (var contender = inner.AttemptAcquire())
        {
            Assert.False(contender.IsAcquired);
        }
        Assert.Equal(["acquire", "callback"], log);
        // Act
        gate.Release();
        await pending.Guard();
        // Assert
        Assert.Equal(["acquire", "callback", "release"], log);
        Assert.Equal(1, clock.Disposed);
        Assert.Equal(1, inner.GetStatistics()!.CurrentAvailablePermits);
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    [Fact]
    public async Task VoidComposition_AllFourPoliciesPreserveCallerCancellation()
    {
        // Arrange
        using var inner = Concurrency();
        var log = new List<string>();
        using var limiter = new TracingRateLimiter(inner, log);
        using var gate = new AsyncGate();
        using var caller = new CancellationTokenSource();
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        async ValueTask callback(CancellationToken token)
        {
            calls++;
            log.Add("callback");
            using var observer = token.Register(() => canceled.TrySetResult());
            await gate.Wait().Guard();
            token.ThrowIfCancellationRequested();
        }
        var operation = AllFour(callback, limiter, clock, breaker);
        // Act
        var pending = operation(caller.Token).AsTask();
        await gate.Entered;
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        caller.Cancel();
        await canceled.Task.Guard();
        gate.Release();
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.Guard());
        Assert.Equal(1, calls);
        Assert.Equal(["acquire", "callback", "release"], log);
        Assert.Equal(1, clock.Disposed);
        Assert.Equal(1, inner.GetStatistics()!.CurrentAvailablePermits);
        // Act
        await operation(default).Guard();
        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(2, clock.Disposed);
        Assert.Equal(["acquire", "callback", "release", "acquire", "callback", "release"], log);
    }

    [Fact]
    public async Task VoidComposition_AllFourPoliciesHaveObservableFailureOrder()
    {
        // Arrange
        using var inner = Concurrency();
        var log = new List<string>();
        using var limiter = new TracingRateLimiter(inner, log);
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        int calls = 0;
        ValueTask callback(CancellationToken _)
        {
            log.Add($"callback-{++calls}");
            Assert.Equal(calls, clock.Created);
            Assert.Equal(calls - 1, clock.Disposed);
            if (calls == 1)
            {
                throw new InvalidOperationException("retried inside breaker");
            }
            return ValueTask.CompletedTask;
        }
        // Act
        await AllFour(callback, limiter, clock, breaker)(default).Guard();
        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(["acquire", "callback-1", "release", "acquire", "callback-2", "release"], log);
        Assert.Equal(2, clock.Created);
        Assert.Equal(2, clock.Disposed);
        Assert.Equal(1, inner.GetStatistics()!.CurrentAvailablePermits);
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    private static Func<CancellationToken, ValueTask> AllFour(Func<CancellationToken, ValueTask> operation,
        System.Threading.RateLimiting.RateLimiter limiter, RecordingTimeProvider clock, CircuitBreakerResilience breaker)
        => operation.RateLimitedBy(limiter, 1).TimedOutAfter(Period, clock)
            .Retryable(new() { MaxRetries = 1 }).CircuitBrokenBy(breaker);
}
