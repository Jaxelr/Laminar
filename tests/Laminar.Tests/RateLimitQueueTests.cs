namespace Laminar.Tests;

public sealed class RateLimitQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_QueueAdmitsOnlyAfterHeldSuccessOrFailureReleasesPermit(bool fail)
    {
        // Arrange
        using var limiter = Concurrency(1, 1);
        using var gate = new AsyncGate();
        var policy = new RateLimitResilience(limiter);
        var expected = new InvalidOperationException("held");
        int heldCalls = 0;
        int queuedCalls = 0;
        // Act
        var held = policy.Apply<object>(async _ =>
        {
            heldCalls++;
            await gate.Wait().Guard();
            if (fail)
            {
                throw expected;
            }
            return Sentinel;
        })(default).AsTask();
        await gate.Entered;
        var next = policy.Apply(Success(() => queuedCalls++))(default).AsTask();
        // Assert
        Assert.Equal(1, limiter.GetStatistics()!.CurrentQueuedCount);
        Assert.False(next.IsCompleted);
        Assert.Equal(0, queuedCalls);
        await Assert.ThrowsAsync<RateLimitRejectedException>(
            async () => await policy.Apply(Success(() => queuedCalls++))(default).Guard());
        // Act
        gate.Release();
        // Assert
        if (fail)
        {
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await held.Guard()));
        }
        else
        {
            Assert.Same(Sentinel, await held.Guard());
        }
        Assert.Same(Sentinel, await next.Guard());
        Assert.Equal(1, heldCalls);
        Assert.Equal(1, queuedCalls);
        Assert.Equal(0, limiter.GetStatistics()!.CurrentQueuedCount);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task Apply_CanceledQueuedCallDoesNotConsumeFutureCapacity()
    {
        // Arrange
        using var limiter = Concurrency(1, 1);
        using var gate = new AsyncGate();
        using var caller = new CancellationTokenSource();
        var policy = new RateLimitResilience(limiter);
        int calls = 0;
        // Act
        var held = policy.Apply<object>(gate.Result)(default).AsTask();
        await gate.Entered;
        var queued = policy.Apply(Success(() => calls++))(caller.Token).AsTask();
        // Assert
        Assert.Equal(1, limiter.GetStatistics()!.CurrentQueuedCount);
        Assert.False(queued.IsCompleted);
        // Act
        caller.Cancel();
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued.Guard());
        Assert.Equal(0, calls);
        // Act
        gate.Release();
        // Assert
        Assert.Same(Sentinel, await held.Guard());
        Assert.Same(Sentinel, await policy.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(1, calls);
        Assert.Equal(0, limiter.GetStatistics()!.CurrentQueuedCount);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
    }
}
