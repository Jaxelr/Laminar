namespace Laminar.Tests;

public sealed class RateLimitResilienceTests
{
    [Fact]
    public async Task Apply_AvailablePermitReturnsResultAndExactToken()
    {
        // Arrange
        using var limiter = Concurrency(3);
        using var caller = new CancellationTokenSource();
        int calls = 0;
        CancellationToken observed = default;
        var operation = new RateLimitResilience(limiter, 2).Apply<object>(token =>
        {
            calls++;
            observed = token;
            Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(caller.Token).Guard());
        Assert.Equal(caller.Token, observed);
        Assert.Equal(1, calls);
        Assert.Equal(3, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_WeightedPermitHeldUntilSuccessOrFailure(bool fail)
    {
        // Arrange
        using var limiter = Concurrency(3);
        using var gate = new AsyncGate();
        int calls = 0;
        var expected = new InvalidOperationException("callback");
        var policy = new RateLimitResilience(limiter, 2);
        var operation = policy.Apply<object>(async _ =>
        {
            calls++;
            await gate.Wait().Guard();
            if (fail)
            {
                throw expected;
            }
            return Sentinel;
        });
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
        var rejected = await Assert.ThrowsAsync<RateLimitRejectedException>(
            async () => await operation(default).Guard());
        Assert.Null(rejected.RetryAfter);
        Assert.Equal(1, calls);
        // Act
        gate.Release();
        // Assert
        if (fail)
        {
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending.Guard()));
        }
        else
        {
            Assert.Same(Sentinel, await pending.Guard());
        }
        Assert.Equal(3, limiter.GetStatistics()!.CurrentAvailablePermits);
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success())(default).Guard());
        Assert.Equal(3, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_RejectedLeasePreservesMetadataAndDisposes(bool withMetadata)
    {
        // Arrange
        using var limiter = new ControlledRateLimiter
        {
            Acquired = false,
            RetryAfter = withMetadata ? TimeSpan.FromSeconds(7) : null,
        };
        int calls = 0;
        var operation = new RateLimitResilience(limiter, 2).Apply(Success(() => calls++));
        // Act
        var exception = await Assert.ThrowsAsync<RateLimitRejectedException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Equal(withMetadata ? TimeSpan.FromSeconds(7) : (TimeSpan?) null, exception.RetryAfter);
        Assert.Equal(0, calls);
        Assert.Equal(2, limiter.Permits);
        Assert.Equal(1, limiter.Acquisitions);
        Assert.Equal(1, limiter.Disposals);
    }

    [Fact]
    public async Task Apply_CancellationAfterAcquisitionSkipsCallbackAndDisposesLease()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        using var limiter = new ControlledRateLimiter { OnAcquire = caller.Cancel };
        int calls = 0;
        var operation = new RateLimitResilience(limiter).Apply(Success(() => calls++));
        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await operation(caller.Token).Guard());
        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
        Assert.Equal(1, limiter.Acquisitions);
        Assert.Equal(1, limiter.Disposals);
    }

    [Fact]
    public async Task Apply_PreCanceledCallerDoesNotAcquireLease()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        using var limiter = new ControlledRateLimiter();
        int calls = 0;
        caller.Cancel();
        var operation = new RateLimitResilience(limiter).Apply(Success(() => calls++));
        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation(caller.Token).Guard());
        Assert.Equal(0, calls);
        Assert.Equal(0, limiter.Acquisitions);
        Assert.Equal(0, limiter.Disposals);
    }

    [Fact]
    public async Task Apply_TwoDelegatesShareLimiterCapacity()
    {
        // Arrange
        using var limiter = Concurrency();
        using var gate = new AsyncGate();
        var first = new RateLimitResilience(limiter).Apply<object>(gate.Result);
        int secondCalls = 0;
        var second = new RateLimitResilience(limiter).Apply(Success(() => secondCalls++));
        // Act
        var pending = first(default).AsTask();
        await gate.Entered;
        // Assert
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await second(default).Guard());
        Assert.Equal(0, secondCalls);
        // Act
        gate.Release();
        // Assert
        Assert.Same(Sentinel, await pending.Guard());
        Assert.Same(Sentinel, await second(default).Guard());
        Assert.Equal(1, secondCalls);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
    }
}
