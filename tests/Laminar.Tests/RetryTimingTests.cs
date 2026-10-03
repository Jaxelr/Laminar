namespace Laminar.Tests;

public sealed class RetryTimingTests
{
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

    [Fact]
    public async Task Apply_CancellationDuringRegisteredDelayStopsRetry()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        int calls = 0;
        var operation = new RetryResilience(new() { Delay = Period, TimeProvider = clock }).Apply<object>(_ =>
        {
            calls++;
            throw new InvalidOperationException("transient");
        });
        // Act
        var pending = operation(caller.Token).AsTask();
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        caller.Cancel();
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.Guard());
        Assert.Equal(1, calls);
        Assert.Equal(1, clock.Disposed);
        // Act
        clock.Advance(Period);
        // Assert
        Assert.Equal(1, calls);
    }
}
