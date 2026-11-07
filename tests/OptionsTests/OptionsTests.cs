namespace Laminar.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void RetryOptions_DefaultsMatchContract()
    {
        // Arrange
        // Act
        var options = new RetryOptions();
        // Assert
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.Zero, options.Delay);
        Assert.Same(TimeProvider.System, options.TimeProvider);
        Assert.True(options.ShouldHandle(new InvalidOperationException()));
        Assert.False(options.ShouldHandle(new OperationCanceledException()));
    }

    [Fact]
    public void CircuitBreakerOptions_DefaultsMatchContract()
    {
        // Arrange
        // Act
        var options = new CircuitBreakerOptions();
        // Assert
        Assert.Equal(5, options.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), options.BreakDuration);
        Assert.Same(TimeProvider.System, options.TimeProvider);
        Assert.True(options.ShouldHandle(new InvalidOperationException()));
        Assert.False(options.ShouldHandle(new CircuitOpenException()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Options_CustomValuesControlPolicyBehavior(bool circuit)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        Func<Exception, bool> predicate = _ => false;
        // Act
        var retry = new RetryOptions { MaxRetries = 2, Delay = Period, TimeProvider = clock, ShouldHandle = predicate };
        var breaker = new CircuitBreakerOptions { FailureThreshold = 2, BreakDuration = Period, TimeProvider = clock, ShouldHandle = predicate };
        // Assert
        Assert.Equal(2, retry.MaxRetries);
        Assert.Equal(2, breaker.FailureThreshold);
        Assert.Equal(Period, retry.Delay);
        Assert.Equal(Period, breaker.BreakDuration);
        Assert.Same(clock, retry.TimeProvider);
        Assert.Same(clock, breaker.TimeProvider);
        Assert.Same(predicate, retry.ShouldHandle);
        Assert.Same(predicate, breaker.ShouldHandle);
        // Arrange
        IOperationResilience policy = circuit ? new CircuitBreakerResilience(breaker) : new RetryResilience(retry);
        var expected = new InvalidOperationException();
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await policy.Apply(Failure(expected))(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(0, clock.Created);
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success())(default).Guard());
    }
}
