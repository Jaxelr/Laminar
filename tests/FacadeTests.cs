using static Laminar.CircuitBreaker;
using static Laminar.RateLimit;
using static Laminar.Timeouts;

namespace Laminar.Tests;

public sealed class FacadeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedOutAfter_ReturnsResultAndUsesDeadlineToken(bool tokenAware)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        CancellationToken forwarded = default;
        Task<object> Callback(CancellationToken token)
        {
            forwarded = token;
            return Task.FromResult(Sentinel);
        }

        // Act
        object result = tokenAware
            ? await TimedOutAfter(token => Callback(token), Period, clock, caller.Token).Guard()
            : await TimedOutAfter(() => Callback(default), Period, clock, caller.Token).Guard();

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(tokenAware, forwarded.CanBeCanceled);
        Assert.NotEqual(caller.Token, forwarded);
        Assert.Equal(Period, await clock.Registered());
        Assert.Equal(1, clock.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedOutAfter_WaitsForLateVoidOperationBeforeRejecting(bool tokenAware)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var operation = new PendingOperation();

        // Act
        var pending = tokenAware
            ? TimedOutAfter(token => operation.RunAsync(token), Period, clock).AsTask()
            : TimedOutAfter(() => operation.RunAsync(default), Period, clock).AsTask();
        await operation.Entered;
        clock.Advance(Period);

        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(tokenAware, operation.Token.IsCancellationRequested);

        // Act
        operation.Release();
        var exception = await Assert.ThrowsAsync<TimeoutRejectedException>(async () => await pending.Guard());

        // Assert
        Assert.Equal(Period, exception.Timeout);
        Assert.Equal(1, clock.Disposed);
    }

    [Fact]
    public async Task TimedOutAfter_CallerCancellationRemainsCancellation()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await TimedOutAfter(() => Task.FromResult(++calls), Period, clock, caller.Token).Guard());

        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
        Assert.Equal(0, clock.Created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RateLimitedBy_HoldsWeightedPermitAndReturnsResult(bool tokenAware)
    {
        // Arrange
        using var limiter = Concurrency(3);
        using var caller = new CancellationTokenSource();
        using var operation = new PendingOperation();

        // Act
        var pending = tokenAware
            ? RateLimitedBy(async token => await operation.ResultAsync(token), limiter, 2, caller.Token).AsTask()
            : RateLimitedBy(async () => await operation.ResultAsync(default), limiter, 2, caller.Token).AsTask();
        await operation.Entered;

        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(tokenAware ? caller.Token : default, operation.Token);
        Assert.Equal(1, limiter.GetStatistics()!.CurrentAvailablePermits);

        // Act
        operation.Release();
        object result = await pending.Guard();

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(3, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RateLimitedBy_RejectsVoidOperationAndPreservesMetadata(bool tokenAware)
    {
        // Arrange
        using var limiter = new ControlledRateLimiter { Acquired = false, RetryAfter = Period };
        int calls = 0;
        Task Callback()
        {
            calls++;
            return Task.CompletedTask;
        }

        // Act
        var pending = tokenAware
            ? RateLimitedBy(_ => Callback(), limiter, 2).AsTask()
            : RateLimitedBy(() => Callback(), limiter, 2).AsTask();
        var exception = await Assert.ThrowsAsync<RateLimitRejectedException>(async () => await pending.Guard());

        // Assert
        Assert.Equal(0, calls);
        Assert.Equal(Period, exception.RetryAfter);
        Assert.Equal(2, limiter.Permits);
        Assert.Equal(1, limiter.Disposals);
    }

    [Fact]
    public async Task RateLimitedBy_ReusesCallerLimiterWithoutDisposingIt()
    {
        // Arrange
        using var limiter = Concurrency();
        using var gate = new AsyncGate();
        var pending = RateLimitedBy(async () => await gate.Wait(), limiter).AsTask();
        await gate.Entered;
        int rejectedCalls = 0;

        // Act
        await Assert.ThrowsAsync<RateLimitRejectedException>(async () =>
            await RateLimitedBy(() => Task.FromResult(++rejectedCalls), limiter).Guard());
        gate.Release();
        await pending.Guard();
        await RateLimitedBy(async _ => await Task.Yield(), limiter).Guard();

        // Assert
        Assert.Equal(0, rejectedCalls);
        using var lease = limiter.AttemptAcquire();
        Assert.True(lease.IsAcquired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CircuitBrokenBy_ResultCallsInferTypeAndForwardToken(bool tokenAware)
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider());
        using var caller = new CancellationTokenSource();
        CancellationToken forwarded = default;
        Task<object> Callback(CancellationToken token)
        {
            forwarded = token;
            return Task.FromResult(Sentinel);
        }

        // Act
        object result = tokenAware
            ? await CircuitBrokenBy(async token => await Callback(token), breaker, caller.Token).Guard()
            : await CircuitBrokenBy(async () => await Callback(default), breaker, caller.Token).Guard();

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(tokenAware ? caller.Token : default, forwarded);
    }

    [Fact]
    public async Task CircuitBrokenBy_ReusesBreakerAcrossResultCallsAndRecovers()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, threshold: 1);
        var expected = new InvalidOperationException("failure");

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CircuitBrokenBy(() => Task.FromException<object>(expected), breaker).Guard());

        // Assert
        Assert.Same(expected, actual);
        await Assert.ThrowsAsync<CircuitOpenException>(async () =>
            await CircuitBrokenBy(() => Task.FromResult(Sentinel), breaker).Guard());

        // Act
        clock.Advance(Period);
        object result = await CircuitBrokenBy(() => Task.FromResult(Sentinel), breaker).Guard();

        // Assert
        Assert.Same(Sentinel, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CircuitBrokenBy_VoidCallsAwaitCompletionAndForwardToken(bool tokenAware)
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider());
        using var caller = new CancellationTokenSource();
        using var operation = new PendingOperation();

        // Act
        var pending = tokenAware
            ? CircuitBrokenBy(async token => await operation.RunAsync(token), breaker, caller.Token).AsTask()
            : CircuitBrokenBy(async () => await operation.RunAsync(default), breaker, caller.Token).AsTask();
        await operation.Entered;

        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(tokenAware ? caller.Token : default, operation.Token);

        // Act
        operation.Release();
        await pending.Guard();
    }

    [Fact]
    public void Facades_ValidateCallbacksBeforeExecution()
    {
        // Arrange
        Func<Task> missing = null!;
        using var limiter = Concurrency();
        var breaker = Breaker(new RecordingTimeProvider());

        // Act
        var timeout = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = TimedOutAfter(missing, Period).AsTask();
        });
        var rateLimit = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = RateLimitedBy(missing, limiter).AsTask();
        });
        var circuitBreaker = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = CircuitBrokenBy(missing, breaker).AsTask();
        });

        // Assert
        Assert.Equal("operation", timeout.ParamName);
        Assert.Equal("operation", rateLimit.ParamName);
        Assert.Equal("operation", circuitBreaker.ParamName);
    }

    [Fact]
    public void Facades_ValidateDependenciesBeforeExecution()
    {
        // Arrange
        static Task Operation()
        {
            return Task.CompletedTask;
        }

        // Act
        var timeout = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = TimedOutAfter(Operation, TimeSpan.Zero).AsTask();
        });
        var rateLimiter = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = RateLimitedBy(Operation, null!).AsTask();
        });
        var circuitBreaker = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = CircuitBrokenBy(Operation, null!).AsTask();
        });

        // Assert
        Assert.Equal("timeout", timeout.ParamName);
        Assert.Equal("limiter", rateLimiter.ParamName);
        Assert.Equal("resilience", circuitBreaker.ParamName);
    }

    private sealed class PendingOperation : IDisposable
    {
        private readonly AsyncGate gate = new();

        internal CancellationToken Token { get; private set; }
        internal Task Entered => gate.Entered;

        internal async Task RunAsync(CancellationToken token)
        {
            Token = token;
            await gate.Wait();
        }

        internal async Task<object> ResultAsync(CancellationToken token)
        {
            await RunAsync(token);
            return Sentinel;
        }

        internal void Release() => gate.Release();
        public void Dispose() => gate.Dispose();
    }
}
