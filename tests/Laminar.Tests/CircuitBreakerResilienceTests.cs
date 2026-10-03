namespace Laminar.Tests;

public sealed class CircuitBreakerResilienceTests
{
    [Fact]
    public async Task Apply_OpensAfterConsecutiveFailureThreshold()
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider());
        int calls = 0;
        var expected = new InvalidOperationException("handled");
        var operation = breaker.Apply<object>(_ =>
        {
            calls++;
            throw expected;
        });
        for (int i = 0; i < 2; i++)
        {
            // Act
            // Assert
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await operation(default).Guard()));
            Assert.Equal(i + 1, calls);
        }
        // Act
        // Assert
        await Assert.ThrowsAsync<CircuitOpenException>(async () => await operation(default).Guard());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Apply_SuccessResetsConsecutiveFailures()
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider());
        await Fail(breaker, 1);
        int calls = 0;
        var operation = breaker.Apply(Success(() => calls++));
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        // Act
        await Fail(breaker, 1);
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Apply_RejectsBeforeAndAdmitsAtExactBreakBoundary()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock);
        // Act
        await Fail(breaker);
        // Assert
        await Rejected(breaker);
        // Act
        clock.Advance(Period - TimeSpan.FromTicks(1));
        // Assert
        await Rejected(breaker);
        int probes = 0;
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success(() => probes++))(default).Guard());
        Assert.Equal(1, probes);
        Assert.Equal(0, clock.Created);
    }

    [Fact]
    public async Task Apply_HalfOpenProbeIsExclusiveAndSuccessClosesAndResets()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock);
        using var gate = new AsyncGate();
        // Act
        await Fail(breaker);
        clock.Advance(Period);
        var probe = breaker.Apply<object>(gate.Result)(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(probe.IsCompleted);
        await Rejected(breaker);
        // Act
        gate.Release();
        // Assert
        Assert.Same(Sentinel, await probe.Guard());
        // Act
        await Fail(breaker, 1);
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
        // Act
        await Fail(breaker);
        // Assert
        await Rejected(breaker);
    }

    [Fact]
    public async Task Apply_FailedProbeRequiresFreshFullBreakInterval()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock);
        // Act
        await Fail(breaker);
        clock.Advance(Period + TimeSpan.FromSeconds(3));
        await Fail(breaker, 1);
        // Assert
        await Rejected(breaker);
        // Act
        clock.Advance(Period - TimeSpan.FromTicks(1));
        // Assert
        await Rejected(breaker);
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        int calls = 0;
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_CanceledOrUnhandledProbeReleasesSlotWithoutCounting(bool canceled)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, predicate: exception => canceled || exception is InvalidOperationException);
        using var caller = new CancellationTokenSource();
        using var gate = new AsyncGate();
        await Fail(breaker);
        clock.Advance(Period);
        Exception expected = canceled ? new OperationCanceledException("probe") : new ArgumentException("excluded");
        // Act
        var probe = breaker.Apply<object>(async _ =>
        {
            await gate.Wait().Guard();
            throw expected;
        })(caller.Token).AsTask();
        await gate.Entered;
        // Assert
        await Rejected(breaker);
        // Act
        if (canceled)
        {
            caller.Cancel();
        }
        gate.Release();
        var actual = await Record.ExceptionAsync(async () => await probe.Guard());
        // Assert
        Assert.Same(expected, actual);
        int replacements = 0;
        // Act
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success(() => replacements++))(default).Guard());
        Assert.Equal(1, replacements);
        // Act
        await Fail(breaker, 1);
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    [Fact]
    public async Task Apply_DelegatesShareBreakerState()
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider(), 1);
        int firstCalls = 0;
        int secondCalls = 0;
        var expected = new InvalidOperationException();
        var first = breaker.Apply<object>(_ =>
        {
            firstCalls++;
            throw expected;
        });
        var second = breaker.Apply(Success(() => secondCalls++));
        // Act
        // Assert
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first(default).Guard()));
        await Assert.ThrowsAsync<CircuitOpenException>(async () => await second(default).Guard());
        Assert.Equal(1, firstCalls);
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public async Task Apply_CancellationDuringAcceptedFailureDoesNotCount()
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider(), 1, _ => true);
        using var caller = new CancellationTokenSource();
        var expected = new InvalidOperationException("canceled caller");
        int calls = 0;
        var operation = breaker.Apply<object>(_ =>
        {
            calls++;
            caller.Cancel();
            throw expected;
        });
        // Act
        // Assert
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(caller.Token).Guard()));
        Assert.Same(Sentinel, await breaker.Apply(Success(() => calls++))(default).Guard());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Apply_PreCanceledCallerSkipsCallbackWithoutOpening()
    {
        // Arrange
        var breaker = Breaker(new RecordingTimeProvider(), 1, _ => true);
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;
        var operation = breaker.Apply(Success(() => calls++));
        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation(caller.Token).Guard());
        Assert.Equal(0, calls);
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(1, calls);
    }
}
