namespace Laminar.Tests;

public sealed class TimeoutResilienceTests
{
    [Fact]
    public async Task Apply_ResultBeforeDeadlineIsReturned()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        int calls = 0;
        CancellationToken forwarded = default;
        using var caller = new CancellationTokenSource();
        var operation = new TimeoutResilience(Period, clock).Apply<object>(token =>
        {
            calls++;
            forwarded = token;
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(caller.Token).Guard());
        Assert.True(forwarded.CanBeCanceled);
        Assert.NotEqual(caller.Token, forwarded);
        Assert.False(forwarded.IsCancellationRequested);
        Assert.Equal(1, calls);
        Assert.Equal(1, clock.Disposed);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(10000d)]
    [InlineData(4294967294d)]
    public async Task Apply_CooperativeDeadlinePreservesTokenPayloadAndInnerIdentity(double milliseconds)
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(milliseconds);
        var clock = new RecordingTimeProvider();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new OperationCanceledException("cooperative deadline");
        CancellationToken forwarded = default;
        int calls = 0;
        var operation = new TimeoutResilience(timeout, clock).Apply<object>(async token =>
        {
            calls++;
            forwarded = token;
            using var observer = token.Register(() => canceled.TrySetResult());
            await canceled.Task.Guard();
            throw expected;
        });
        // Act
        var pending = operation(default).AsTask();
        // Assert
        Assert.Equal(timeout, await clock.Registered());
        Assert.Equal(1, calls);
        Assert.False(pending.IsCompleted);
        // Act
        clock.Advance(timeout);
        await canceled.Task.Guard();
        var exception = await Assert.ThrowsAsync<TimeoutRejectedException>(async () => await pending.Guard());
        // Assert
        Assert.True(forwarded.IsCancellationRequested);
        Assert.Equal(timeout, exception.Timeout);
        Assert.Same(expected, exception.InnerException);
        Assert.Equal(1, clock.Disposed);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_CallerCancellationWinsBeforeOrAfterDeadline(bool deadlineFirst)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        using var gate = new AsyncGate();
        CancellationToken forwarded = default;
        var expected = new OperationCanceledException("caller priority");
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = new TimeoutResilience(Period, clock).Apply<object>(async token =>
        {
            forwarded = token;
            using var observer = token.Register(() => canceled.TrySetResult());
            await gate.Wait().Guard();
            throw expected;
        });
        // Act
        var pending = operation(caller.Token).AsTask();
        await gate.Entered;
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        if (deadlineFirst)
        {
            clock.Advance(Period);
        }
        caller.Cancel();
        await canceled.Task.Guard();
        // Assert
        Assert.True(forwarded.IsCancellationRequested);
        Assert.False(pending.IsCompleted);
        // Act
        gate.Release();
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, clock.Disposed);
    }

    [Fact]
    public async Task Apply_PreCanceledCallerSkipsCallbackAndTimer()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        int calls = 0;
        caller.Cancel();
        var operation = new TimeoutResilience(Period, clock).Apply(Success(() => calls++));
        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await operation(caller.Token).Guard());
        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
        Assert.Equal(0, clock.Created);
    }

    [Fact]
    public async Task Apply_NonCancellationFaultPreservesIdentity()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var expected = new InvalidOperationException("failure");
        int calls = 0;
        var operation = new TimeoutResilience(Period, clock).Apply<object>(_ =>
        {
            calls++;
            throw expected;
        });
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
        Assert.Equal(1, clock.Disposed);
    }

    [Fact]
    public async Task Apply_IgnoredCancellationDoesNotAbandonOperationAndRejectsLateResult()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var gate = new AsyncGate();
        CancellationToken forwarded = default;
        int completions = 0;
        var operation = new TimeoutResilience(Period, clock).Apply<object>(async token =>
        {
            forwarded = token;
            await gate.Wait().Guard();
            completions++;
            return Sentinel;
        });
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        clock.Advance(Period + Period);
        // Assert
        Assert.True(forwarded.IsCancellationRequested);
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, completions);
        // Act
        gate.Release();
        var actual = await Assert.ThrowsAsync<TimeoutRejectedException>(async () => await pending.Guard());
        // Assert
        Assert.Equal(Period, actual.Timeout);
        Assert.IsType<OperationCanceledException>(actual.InnerException);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task Apply_DeadlineFiresAtExactFakeBoundary()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken forwarded = default;
        var operation = new TimeoutResilience(Period, clock).Apply<object>(async token =>
        {
            forwarded = token;
            using var observer = token.Register(() => canceled.TrySetResult());
            await canceled.Task.Guard();
            token.ThrowIfCancellationRequested();
            return Sentinel;
        });
        // Act
        var pending = operation(default).AsTask();
        // Assert
        Assert.Equal(Period, await clock.Registered());
        // Act
        clock.Advance(Period - TimeSpan.FromTicks(1));
        // Assert
        Assert.False(forwarded.IsCancellationRequested);
        Assert.False(canceled.Task.IsCompleted);
        Assert.False(pending.IsCompleted);
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        await canceled.Task.Guard();
        var exception = await Assert.ThrowsAsync<TimeoutRejectedException>(async () => await pending.Guard());
        // Assert
        Assert.True(forwarded.IsCancellationRequested);
        Assert.Equal(Period, exception.Timeout);
        Assert.Equal(1, clock.Disposed);
    }
}
