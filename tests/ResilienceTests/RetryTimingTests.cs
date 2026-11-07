namespace Laminar.Tests;

public sealed class RetryTimingTests
{
    [Theory]
    [InlineData(1d, 2d, 2d)]
    [InlineData(2d, 4d, 8d)]
    [InlineData(1.5d, 3d, 4.5d)]
    public async Task Apply_BackoffFactorControlsRetryDelays(double factor, double second, double third)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        int calls = 0;
        var operation = new RetryResilience(new()
        {
            Delay = TimeSpan.FromSeconds(2),
            BackoffFactor = factor,
            TimeProvider = clock,
        }).Apply<object>(_ => ++calls < 4
            ? throw new InvalidOperationException("transient") : ValueTask.FromResult(Sentinel));
        // Act
        var pending = operation(default).AsTask();
        foreach (double seconds in new[] { 2d, second, third })
        {
            var delay = TimeSpan.FromSeconds(seconds);
            // Assert
            Assert.Equal(delay, await clock.Registered());
            // Act
            clock.Advance(delay);
        }
        // Assert
        Assert.Same(Sentinel, await pending.Guard());
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task Apply_ExponentialDelayResetsForEachInvocation()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        int calls = 0;
        var operation = new RetryResilience(new()
        {
            MaxRetries = 1,
            Delay = Period,
            BackoffFactor = 2,
            TimeProvider = clock,
        }).Apply<object>(_ => ++calls % 2 != 0
            ? throw new InvalidOperationException("transient") : ValueTask.FromResult(Sentinel));
        for (int invocation = 0; invocation < 2; invocation++)
        {
            // Act
            var pending = operation(default).AsTask();
            // Assert
            Assert.Equal(Period, await clock.Registered());
            // Act
            clock.Advance(Period);
            // Assert
            Assert.Same(Sentinel, await pending.Guard());
        }
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task Apply_ExponentialDelayCapsAtTimerLimitWithoutOverflow()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var maximum = PolicyValidationTests.Maximum;
        var operation = new RetryResilience(new()
        {
            Delay = maximum,
            BackoffFactor = double.MaxValue,
            TimeProvider = clock,
        }).Apply(Failure(new InvalidOperationException("exhausted")));
        // Act
        var pending = operation(default).AsTask();
        for (int retry = 0; retry < 3; retry++)
        {
            // Assert
            Assert.Equal(maximum, await clock.Registered());
            // Act
            clock.Advance(maximum);
        }
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending.Guard());
        Assert.Equal(3, clock.Created);
        Assert.Equal(3, clock.Disposed);
    }

    [Fact]
    public async Task Apply_ZeroDelayRemainsImmediateWithLargeBackoffFactor()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        int calls = 0;
        var operation = new RetryResilience(new()
        {
            BackoffFactor = double.MaxValue,
            TimeProvider = clock,
        }).Apply<object>(_ => ++calls < 4
            ? throw new InvalidOperationException("transient") : ValueTask.FromResult(Sentinel));
        // Act
        object result = await operation(default).Guard();
        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(4, calls);
        Assert.Equal(0, clock.Created);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(10000d)]
    [InlineData(4294967294d)]
    public async Task Apply_RetryStartsOnlyAtExactDelayBoundary(double milliseconds)
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        var clock = new RecordingTimeProvider();
        int calls = 0;
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = new RetryResilience(new() { Delay = delay, TimeProvider = clock }).Apply<object>(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("first");
            }
            retried.TrySetResult();
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        var pending = operation(default).AsTask();
        // Assert
        Assert.Equal(delay, await clock.Registered());
        // Act
        clock.Advance(delay - TimeSpan.FromTicks(1));
        // Assert
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.False(retried.Task.IsCompleted);
        Assert.False(pending.IsCompleted);
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        await retried.Task.Guard();
        // Assert
        Assert.Same(Sentinel, await pending.Guard());
        Assert.Equal(2, calls);
        Assert.Equal(1, clock.Created);
        Assert.Equal(1, clock.Disposed);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(2d)]
    public async Task Apply_CancellationDuringRegisteredDelayStopsRetry(double factor)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        int calls = 0;
        var operation = new RetryResilience(new()
        {
            Delay = Period,
            BackoffFactor = factor,
            TimeProvider = clock,
        }).Apply<object>(_ =>
        {
            calls++;
            throw new InvalidOperationException("transient");
        });
        // Act
        var pending = operation(caller.Token).AsTask();
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        clock.Advance(Period);
        // Assert
        Assert.Equal(Period * factor, await clock.Registered());
        // Act
        caller.Cancel();
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.Guard());
        Assert.Equal(2, calls);
        Assert.Equal(2, clock.Disposed);
        // Act
        clock.Advance(Period);
        // Assert
        Assert.Equal(2, calls);
    }
}
