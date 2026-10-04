namespace Laminar.Tests;

public sealed class PolicyValidationTests
{
    internal static readonly TimeSpan Maximum = TimeSpan.FromMilliseconds((double) uint.MaxValue - 1);
    internal static readonly TimeSpan AboveMaximum = TimeSpan.FromMilliseconds((double) uint.MaxValue);

    public static TheoryData<string, string, bool> InvalidCases => new()
    {
        { "rate-null", "limiter", true },
        { "rate-zero", "permitCount", false },
        { "rate-negative", "permitCount", false },
        { "retry-null", "options", true },
        { "retry-count", "options.MaxRetries", false },
        { "retry-negative", "Delay", false },
        { "retry-high", "Delay", false },
        { "retry-predicate", "options.ShouldHandle", true },
        { "retry-provider", "options.TimeProvider", true },
        { "timeout-zero", "timeout", false },
        { "timeout-negative", "timeout", false },
        { "timeout-high", "timeout", false },
        { "circuit-zero", "this.options.FailureThreshold", false },
        { "circuit-count", "this.options.FailureThreshold", false },
        { "circuit-duration-zero", "BreakDuration", false },
        { "circuit-negative", "BreakDuration", false },
        { "circuit-high", "BreakDuration", false },
        { "circuit-predicate", "this.options.ShouldHandle", true },
        { "circuit-provider", "this.options.TimeProvider", true },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void PolicyConstructor_InvalidArgumentsThrowImmediately(string key, string parameter, bool isNull)
    {
        // Arrange
        using var limiter = Concurrency();
        // Act
        ArgumentException exception = isNull
            ? Assert.Throws<ArgumentNullException>(() => Construct(key, limiter))
            : Assert.Throws<ArgumentOutOfRangeException>(() => Construct(key, limiter));
        // Assert
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    [InlineData("rate")]
    [InlineData("retry")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    public void Apply_NullOperationThrowsImmediately(string kind)
    {
        // Arrange
        using var limiter = Concurrency();
        IOperationResilience policy = kind switch
        {
            "rate" => new RateLimitResilience(limiter),
            "retry" => new RetryResilience(new()),
            "timeout" => new TimeoutResilience(Period),
            _ => new CircuitBreakerResilience(),
        };
        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => policy.Apply<object>(null!));
        // Assert
        Assert.Equal("operation", exception.ParamName);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    public async Task PolicyConstructor_MaximumTimerDurationIsAccepted(string kind)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        IOperationResilience policy = kind switch
        {
            "retry" => new RetryResilience(new() { Delay = Maximum, TimeProvider = clock }),
            "timeout" => new TimeoutResilience(Maximum, clock),
            _ => new CircuitBreakerResilience(new() { BreakDuration = Maximum, TimeProvider = clock }),
        };
        int calls = 0;
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(1, calls);
        Assert.Equal(clock.Created, clock.Disposed);
    }

    [Fact]
    public async Task TimeoutConstructor_NullProviderUsesSystem()
    {
        // Arrange
        int calls = 0;
        var policy = new TimeoutResilience(Period, null);
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CircuitConstructor_OmittedOrNullOptionsUsesDefaults(bool explicitNull)
    {
        // Arrange
        var policy = explicitNull ? new CircuitBreakerResilience(null) : new CircuitBreakerResilience();
        // Act
        await Fail(policy, 4);
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success())(default).Guard());
        // Act
        await Fail(policy, 5);
        // Assert
        await Rejected(policy);
    }

    [Fact]
    public async Task RetryConstructor_ZeroRetriesAndDelayAreAccepted()
    {
        // Arrange
        var policy = new RetryResilience(new() { MaxRetries = 0, Delay = TimeSpan.Zero });
        int calls = 0;
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RateLimitConstructor_OnePermitIsAccepted()
    {
        // Arrange
        using var limiter = Concurrency();
        var policy = new RateLimitResilience(limiter, 1);
        // Act
        // Assert
        Assert.Same(Sentinel, await policy.Apply(Success())(default).Guard());
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task CircuitConstructor_ThresholdOneIsAccepted()
    {
        // Arrange
        var policy = Breaker(new RecordingTimeProvider(), 1);
        // Act
        await Fail(policy, 1);
        // Assert
        await Rejected(policy);
    }

    internal static RetryOptions Retry(string key) => key switch
    {
        "retry-null" => null!,
        "retry-count" => new() { MaxRetries = -1 },
        "retry-negative" => new() { Delay = TimeSpan.FromTicks(-1) },
        "retry-high" => new() { Delay = AboveMaximum },
        "retry-predicate" => new() { ShouldHandle = null! },
        "retry-provider" => new() { TimeProvider = null! },
        _ => new(),
    };

    internal static TimeSpan Timeout(string key) => key switch
    {
        "timeout-zero" => TimeSpan.Zero,
        "timeout-negative" => TimeSpan.FromTicks(-1),
        "timeout-high" => AboveMaximum,
        _ => Period,
    };

    private static void Construct(string key, System.Threading.RateLimiting.RateLimiter limiter) => _ = key.Split('-')[0] switch
    {
        "rate" => (IOperationResilience) new RateLimitResilience(
            key == "rate-null" ? null! : limiter, key == "rate-zero" ? 0 : key == "rate-negative" ? -1 : 1),
        "retry" => new RetryResilience(Retry(key)),
        "timeout" => new TimeoutResilience(Timeout(key)),
        _ => new CircuitBreakerResilience(Circuit(key)),
    };

    private static CircuitBreakerOptions Circuit(string key) => key switch
    {
        "circuit-zero" => new() { FailureThreshold = 0 },
        "circuit-count" => new() { FailureThreshold = -1 },
        "circuit-duration-zero" => new() { BreakDuration = TimeSpan.Zero },
        "circuit-negative" => new() { BreakDuration = TimeSpan.FromTicks(-1) },
        "circuit-high" => new() { BreakDuration = AboveMaximum },
        "circuit-predicate" => new() { ShouldHandle = null! },
        "circuit-provider" => new() { TimeProvider = null! },
        _ => new(),
    };
}
