namespace Laminar.Tests;

public sealed class DefaultFailureHandlingTests
{
    [Theory]
    [InlineData("cancellation")]
    [InlineData("base-rejection")]
    [InlineData("rate")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    public async Task Retry_DefaultPredicateDoesNotRetryExcludedFailure(string kind)
    {
        // Arrange
        var expected = Excluded(kind);
        int calls = 0;
        var operation = new RetryResilience(new()).Apply<object>(_ =>
        {
            calls++;
            throw expected;
        });
        // Act
        var actual = await Record.ExceptionAsync(async () => await operation(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("cancellation")]
    [InlineData("base-rejection")]
    [InlineData("rate")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    public async Task Breaker_DefaultPredicateDoesNotCountExcludedFailure(string kind)
    {
        // Arrange
        var policy = Breaker(new RecordingTimeProvider(), 1);
        var expected = Excluded(kind);
        int calls = 0;
        var operation = policy.Apply<object>(_ =>
        {
            calls++;
            throw expected;
        });
        // Act
        var actual = await Record.ExceptionAsync(async () => await operation(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retry_DefaultPredicateRetriesOrdinaryFailure()
    {
        // Arrange
        int calls = 0;
        var operation = new RetryResilience(new()).Apply<object>(_ =>
        {
            if (++calls == 1)
            {
                throw new InvalidOperationException("ordinary");
            }
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Breaker_DefaultPredicateCountsOrdinaryFailure()
    {
        // Arrange
        var policy = Breaker(new RecordingTimeProvider(), 1);
        // Act
        await Fail(policy, 1);
        // Assert
        await Rejected(policy);
    }

    private static Exception Excluded(string kind) => kind switch
    {
        "cancellation" => new OperationCanceledException("unrelated cancellation"),
        "base-rejection" => new RejectionExceptionTests.CustomRejection("custom rejection"),
        "rate" => new RateLimitRejectedException(),
        "timeout" => new TimeoutRejectedException(Period),
        _ => new CircuitOpenException(),
    };
}
