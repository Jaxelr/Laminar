namespace Laminar.Tests;

public sealed class TokenBucketTests
{
    [Fact]
    public async Task RealTokenBucket_TwoAdmissionsThenDepletion()
    {
        // Arrange
        using var bucket = Bucket();
        int calls = 0;
        var operation = new RateLimitResilience(bucket).Apply(Success(() => calls++));
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(1, bucket.GetStatistics()!.CurrentAvailablePermits);
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(0, bucket.GetStatistics()!.CurrentAvailablePermits);
        // Act
        // Assert
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await operation(default).Guard());
        Assert.Equal(2, calls);
        Assert.Equal(0, bucket.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task RealTokenBucket_ManualReplenishmentPathIsSupportedWithoutClockWait()
    {
        // Arrange
        using var bucket = Bucket();
        int calls = 0;
        var operation = new RateLimitResilience(bucket).Apply(Success(() => calls++));
        // Act
        await operation(default).Guard();
        // Assert
        await operation(default).Guard();
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await operation(default).Guard());
        Assert.Equal(0, bucket.GetStatistics()!.CurrentAvailablePermits);
        // Act
        // Assert
        Assert.True(bucket.TryReplenish());
        Assert.False(bucket.IsAutoReplenishing);
        Assert.Equal(Period, bucket.ReplenishmentPeriod);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void RealTokenBucket_ZeroPeriodIsRejectedByRuntime()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentException>(() => Bucket(TimeSpan.Zero));
        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task ControlledReplenishingLimiter_RefillsAtFakeBoundary()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var limiter = new FakeReplenishingLimiter(clock);
        int calls = 0;
        var operation = new RateLimitResilience(limiter).Apply(Success(() => calls++));
        // Act
        await operation(default).Guard();
        await operation(default).Guard();
        clock.Advance(TimeSpan.FromSeconds(9));
        // Assert
        Assert.False(limiter.TryReplenish());
        var exception = await Assert.ThrowsAsync<RateLimitRejectedException>(
            async () => await operation(default).Guard());
        Assert.Equal(TimeSpan.FromSeconds(1), exception.RetryAfter);
        Assert.Equal(2, calls);
        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        // Assert
        Assert.True(limiter.TryReplenish());
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task RealTokenBucket_RejectionProvidesNonzeroRetryAfter()
    {
        // Arrange
        using var bucket = Bucket(Period);
        int calls = 0;
        var operation = new RateLimitResilience(bucket).Apply(Success(() => calls++));
        // Act
        await operation(default).Guard();
        await operation(default).Guard();
        var exception = await Assert.ThrowsAsync<RateLimitRejectedException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Equal(Period, exception.RetryAfter);
        Assert.Equal(2, calls);
        Assert.Equal(0, bucket.GetStatistics()!.CurrentAvailablePermits);
    }
}
