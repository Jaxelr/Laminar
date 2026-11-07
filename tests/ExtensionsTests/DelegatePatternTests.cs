namespace Laminar.Tests;

public sealed class DelegatePatternTests
{
    public enum Shape
    {
        Func,
        Action,
        TaskResult,
        Task,
        TokenTaskResult,
        TokenTask,
        ValueTaskResult,
        ValueTask
    }

    public enum Pattern
    {
        RateLimit,
        Retry,
        Timeout,
        CircuitBreaker,
        Custom
    }

    public static TheoryData<Shape, Pattern> ShapesAndPatterns
    {
        get
        {
            TheoryData<Shape, Pattern> cases = [];
            foreach (var shape in Enum.GetValues<Shape>())
            {
                foreach (var pattern in Enum.GetValues<Pattern>())
                {
                    cases.Add(shape, pattern);
                }
            }
            return cases;
        }
    }

    public static TheoryData<Shape> Shapes
    {
        get
        {
            TheoryData<Shape> cases = [.. Enum.GetValues<Shape>()];
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ShapesAndPatterns))]
    public async Task DirectPattern_AdaptsLazilyAndReturnsResult(Shape shape, Pattern pattern)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        int calls = 0;
        var guarded = policies.Wrap(shape, pattern, _ =>
        {
            calls++;
            return ValueTask.FromResult(Sentinel);
        });
        Assert.Equal(0, calls);

        // Act
        object result = await guarded(default);

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(1, calls);
        Assert.Equal(pattern == Pattern.RateLimit ? 2 : 0, policies.Limiter.Permits);
        Assert.Equal(pattern == Pattern.Custom ? 1 : 0, policies.Custom.Applies);
    }

    [Theory]
    [MemberData(nameof(ShapesAndPatterns))]
    public async Task DirectPattern_PreCanceledTokenPreventsInvocation(Shape shape, Pattern pattern)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;
        var guarded = policies.Wrap(shape, pattern, _ =>
        {
            calls++;
            return ValueTask.FromResult(Sentinel);
        });

        // Act
        var exception = await Record.ExceptionAsync(async () => await guarded(caller.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(exception, exactMatch: false);
        Assert.Equal(0, calls);
        Assert.Equal(0, policies.Limiter.Acquisitions);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task DirectRetry_RetriesOriginalDelegate(Shape shape)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        int calls = 0;
        var guarded = policies.Wrap(shape, Pattern.Retry, _ =>
            ++calls == 1
                ? ValueTask.FromException<object>(new InvalidOperationException())
                : ValueTask.FromResult(Sentinel));

        // Act
        object result = await guarded(default);

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(2, calls);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task DirectRateLimit_RejectsBeforeInvokingDelegate(Shape shape)
    {
        // Arrange
        using var policies = new DelegatePatterns(permitsAvailable: false);
        int calls = 0;
        var guarded = policies.Wrap(shape, Pattern.RateLimit, _ =>
        {
            calls++;
            return ValueTask.FromResult(Sentinel);
        });

        // Act
        var exception = await Record.ExceptionAsync(async () => await guarded(default));

        // Assert
        Assert.IsType<RateLimitRejectedException>(exception);
        Assert.Equal(0, calls);
        Assert.Equal(2, policies.Limiter.Permits);
        Assert.Equal(1, policies.Limiter.Disposals);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task DirectTimeout_RejectsResultAfterDeadline(Shape shape)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        int calls = 0;
        var guarded = policies.Wrap(shape, Pattern.Timeout, _ =>
        {
            calls++;
            policies.Clock.Advance(Period);
            return ValueTask.FromResult(Sentinel);
        });

        // Act
        var exception = await Record.ExceptionAsync(async () => await guarded(default));

        // Assert
        Assert.Equal(Period, Assert.IsType<TimeoutRejectedException>(exception).Timeout);
        Assert.Equal(1, calls);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task DirectBreaker_OpensOnFailureAndRejectsNextCall(Shape shape)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        var expected = new InvalidOperationException();
        int calls = 0;
        var guarded = policies.Wrap(shape, Pattern.CircuitBreaker, _ =>
        {
            calls++;
            return ValueTask.FromException<object>(expected);
        });

        // Act
        var first = await Record.ExceptionAsync(async () => await guarded(default));
        var second = await Record.ExceptionAsync(async () => await guarded(default));

        // Assert
        Assert.Same(expected, first);
        Assert.IsType<CircuitOpenException>(second);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectTaskPattern_ForwardsTokenAndWaitsForCompletion(bool hasResult)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        using var caller = new CancellationTokenSource();
        using var gate = new AsyncGate();
        CancellationToken observed = default;
        var shape = hasResult ? Shape.TokenTaskResult : Shape.TokenTask;
        var guarded = policies.Wrap(shape, Pattern.Custom, async token =>
        {
            observed = token;
            await gate.Wait();
            return Sentinel;
        });

        // Act
        var pending = guarded(caller.Token).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        // Act
        gate.Release();
        // Assert
        Assert.Same(Sentinel, await pending.Guard());
        Assert.Equal(caller.Token, observed);
    }

    [Theory]
    [MemberData(nameof(ShapesAndPatterns))]
    public void DirectPattern_NullDelegateThrowsAtDecoration(Shape shape, Pattern pattern)
    {
        // Arrange
        using var policies = new DelegatePatterns();

        // Act
        var exception = Record.Exception(() => policies.WrapNull(shape, pattern));

        // Assert
        Assert.Equal("operation", Assert.IsType<ArgumentNullException>(exception).ParamName);
        Assert.Equal(0, policies.Limiter.Acquisitions);
    }

    [Theory]
    [MemberData(nameof(ShapesAndPatterns))]
    public async Task DirectPattern_PreservesOriginalFailure(Shape shape, Pattern pattern)
    {
        // Arrange
        using var policies = new DelegatePatterns();
        var expected = new InvalidOperationException("original");
        int calls = 0;
        var guarded = policies.Wrap(shape, pattern, _ =>
        {
            calls++;
            return ValueTask.FromException<object>(expected);
        });

        // Act
        var exception = await Record.ExceptionAsync(async () => await guarded(default));

        // Assert
        Assert.Same(expected, exception);
        Assert.Equal(pattern == Pattern.Retry ? 2 : 1, calls);
    }

    [Fact]
    public async Task DirectPattern_ComposesAllPatternsWithoutAsOperation()
    {
        // Arrange
        using var policies = new DelegatePatterns();
        Func<string> hello = () => "Hello, world!";
        var guarded = hello.RateLimitedBy(policies.Limiter)
            .Retryable().TimedOutAfter(Period, policies.Clock)
            .CircuitBrokenBy(policies.Breaker).ResilientWith(policies.Custom);

        // Act
        string result = await guarded(default);

        // Assert
        Assert.Equal("Hello, world!", result);
        Assert.Equal(1, policies.Limiter.Acquisitions);
        Assert.Equal(1, policies.Custom.Applies);
        Assert.Same(result, policies.Custom.Result);
    }
}
