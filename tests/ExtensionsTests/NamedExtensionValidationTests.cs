namespace Laminar.Tests;

public sealed class NamedExtensionValidationTests
{
    public static TheoryData<string, bool> InvalidCases
    {
        get
        {
            var rows = new TheoryData<string, bool>();
            foreach (string key in new[] { "rate-null", "rate-zero", "rate-negative", "retry-count",
                "retry-negative", "retry-high", "retry-predicate", "retry-provider",
                "timeout-zero", "timeout-negative", "timeout-high", "circuit-null" })
            {
                rows.Add(key, false);
                rows.Add(key, true);
            }
            return rows;
        }
    }

    [Theory]
    [InlineData("rate", false, false)]
    [InlineData("rate", true, false)]
    [InlineData("rate", false, true)]
    [InlineData("rate", true, true)]
    [InlineData("retry", false, false)]
    [InlineData("retry", true, false)]
    [InlineData("retry", false, true)]
    [InlineData("retry", true, true)]
    [InlineData("timeout", false, false)]
    [InlineData("timeout", true, false)]
    [InlineData("timeout", false, true)]
    [InlineData("timeout", true, true)]
    [InlineData("circuit", false, false)]
    [InlineData("circuit", true, false)]
    public void NamedExtension_NullOperationThrowsImmediately(string kind, bool isVoid, bool explicitArguments)
    {
        // Arrange
        using var limiter = Concurrency();
        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => Decorate(kind, isVoid, explicitArguments, true, limiter));
        // Assert
        Assert.Equal("operation", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void NamedExtension_InvalidArgumentsMatchConstructor(string key, bool isVoid)
    {
        // Arrange
        using var limiter = Concurrency();
        bool isNull = key is "rate-null" or "retry-predicate" or "retry-provider" or "circuit-null";
        string parameter = key switch
        {
            "rate-null" => "limiter",
            "rate-zero" or "rate-negative" => "permitCount",
            "retry-count" => "options.MaxRetries",
            "retry-predicate" => "options.ShouldHandle",
            "retry-provider" => "options.TimeProvider",
            "circuit-null" => "resilience",
            _ => key.StartsWith("timeout") ? "timeout" : "Delay",
        };
        // Act
        ArgumentException exception = isNull
            ? Assert.Throws<ArgumentNullException>(() => Decorate(key, isVoid, true, false, limiter))
            : Assert.Throws<ArgumentOutOfRangeException>(() => Decorate(key, isVoid, true, false, limiter));
        // Assert
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Retryable_OmittedOrNullOptionsUsesDefaultBudget(bool isVoid, bool explicitNull)
    {
        // Arrange
        int calls = 0;
        var expected = new InvalidOperationException();
        Func<CancellationToken, ValueTask> action = _ =>
        {
            calls++;
            throw expected;
        };
        Func<CancellationToken, ValueTask<object>> result = _ =>
        {
            calls++;
            throw expected;
        };
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (isVoid)
            {
                await (explicitNull ? action.Retryable(null) : action.Retryable())(default).Guard();
            }
            else
            {
                await (explicitNull ? result.Retryable(null) : result.Retryable())(default).Guard();
            }
        });
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(4, calls);
    }

    private static void Decorate(string key, bool isVoid, bool explicitArguments, bool nullOperation,
        System.Threading.RateLimiting.RateLimiter limiter)
    {
        Func<CancellationToken, ValueTask> action = nullOperation ? null! : _ => ValueTask.CompletedTask;
        var result = nullOperation ? null! : Success();
        var selected = key == "rate-null" ? null! : limiter;
        int count = key == "rate-zero" ? 0 : key == "rate-negative" ? -1 : 1;
        var breaker = key == "circuit-null" ? null! : new CircuitBreakerResilience();
        if (isVoid)
        {
            _ = key.Split('-')[0] switch
            {
                "rate" => explicitArguments ? action.RateLimitedBy(selected, count) : action.RateLimitedBy(selected),
                "retry" => explicitArguments ? action.Retryable(PolicyValidationTests.Retry(key)) : action.Retryable(),
                "timeout" => explicitArguments ? action.TimedOutAfter(PolicyValidationTests.Timeout(key), TimeProvider.System) : action.TimedOutAfter(Period),
                _ => action.CircuitBrokenBy(breaker),
            };
        }
        else
        {
            _ = key.Split('-')[0] switch
            {
                "rate" => explicitArguments ? result.RateLimitedBy(selected, count) : result.RateLimitedBy(selected),
                "retry" => explicitArguments ? result.Retryable(PolicyValidationTests.Retry(key)) : result.Retryable(),
                "timeout" => explicitArguments ? result.TimedOutAfter(PolicyValidationTests.Timeout(key), TimeProvider.System) : result.TimedOutAfter(Period),
                _ => result.CircuitBrokenBy(breaker),
            };
        }
    }
}
