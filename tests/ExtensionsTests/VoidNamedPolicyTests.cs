namespace Laminar.Tests;

public sealed class VoidNamedPolicyTests
{
    [Fact]
    public async Task VoidRateLimitedBy_HoldsPermitUntilFullCompletion()
    {
        // Arrange
        using var limiter = Concurrency();
        using var gate = new AsyncGate();
        int calls = 0;
        Func<CancellationToken, ValueTask> callback = async _ =>
        {
            calls++;
            await gate.Wait().Guard();
        };
        var operation = callback.RateLimitedBy(limiter);
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, limiter.GetStatistics()!.CurrentAvailablePermits);
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await operation(default).Guard());
        Assert.Equal(1, calls);
        // Act
        gate.Release();
        await pending.Guard();
        await operation(default).Guard();
        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task VoidRetryable_RetriesFailureThenWaitsForSuccessfulCompletion()
    {
        // Arrange
        using var gate = new AsyncGate();
        int calls = 0;
        Func<CancellationToken, ValueTask> callback = async _ =>
        {
            if (++calls == 1)
            {
                throw new InvalidOperationException("transient");
            }
            await gate.Wait().Guard();
        };
        // Act
        var pending = callback.Retryable(new() { MaxRetries = 1 })(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.Equal(2, calls);
        Assert.False(pending.IsCompleted);
        // Act
        gate.Release();
        await pending.Guard();
        // Assert
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VoidTimeout_PropagatesCallerOrRejectsAtRegisteredDeadline(bool deadline)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken forwarded = default;
        int calls = 0;
        Func<CancellationToken, ValueTask> callback = async token =>
        {
            calls++;
            forwarded = token;
            using var observer = token.Register(() => canceled.TrySetResult());
            await canceled.Task.Guard();
            token.ThrowIfCancellationRequested();
        };
        // Act
        var pending = callback.TimedOutAfter(Period, clock)(caller.Token).AsTask();
        // Assert
        Assert.Equal(Period, await clock.Registered());
        Assert.NotEqual(caller.Token, forwarded);
        // Act
        if (deadline)
        {
            clock.Advance(Period);
        }
        else
        {
            caller.Cancel();
        }
        await canceled.Task.Guard();
        // Assert
        if (deadline)
        {
            var exception = await Assert.ThrowsAsync<TimeoutRejectedException>(async () => await pending.Guard());
            Assert.Equal(Period, exception.Timeout);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.Guard());
        }
        Assert.True(forwarded.IsCancellationRequested);
        Assert.Equal(1, calls);
        Assert.Equal(1, clock.Disposed);
    }

    [Fact]
    public async Task VoidBreaker_OpensRejectsAndRecoversAtExactBoundary()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        bool fail = true;
        int calls = 0;
        var expected = new InvalidOperationException("handled");
        Func<CancellationToken, ValueTask> callback = _ =>
        {
            calls++;
            if (fail)
            {
                throw expected;
            }
            return ValueTask.CompletedTask;
        };
        var operation = callback.CircuitBrokenBy(breaker);
        // Act
        // Assert
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard()));
        await Assert.ThrowsAsync<CircuitOpenException>(async () => await operation(default).Guard());
        // Act
        clock.Advance(Period - TimeSpan.FromTicks(1));
        // Assert
        await Assert.ThrowsAsync<CircuitOpenException>(async () => await operation(default).Guard());
        Assert.Equal(1, calls);
        // Act
        fail = false;
        clock.Advance(TimeSpan.FromTicks(1));
        await operation(default).Guard();
        // Assert
        await operation(default).Guard();
        Assert.Equal(3, calls);
    }
}
