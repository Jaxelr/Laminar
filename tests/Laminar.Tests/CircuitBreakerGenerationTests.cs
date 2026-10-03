namespace Laminar.Tests;

public sealed class CircuitBreakerGenerationTests
{
    [Fact]
    public async Task Apply_StaleSuccessCannotCloseNewOpenGeneration()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        using var staleGate = new AsyncGate();
        // Act
        var stale = breaker.Apply<object>(staleGate.Result)(default).AsTask();
        await staleGate.Entered;
        await Fail(breaker, 1);
        staleGate.Release();
        // Assert
        Assert.Same(Sentinel, await stale.Guard());
        await Rejected(breaker);
        // Act
        clock.Advance(Period);
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_StaleSuccessOrFailureCannotStealExclusiveBoundaryProbe(bool staleFailure)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        using var staleGate = new AsyncGate();
        using var probeGate = new AsyncGate();
        var expected = new InvalidOperationException("stale");
        var probeError = new InvalidOperationException("probe");
        // Act
        var stale = breaker.Apply<object>(async _ =>
        {
            await staleGate.Wait().Guard();
            if (staleFailure)
            {
                throw expected;
            }
            return Sentinel;
        })(default).AsTask();
        await staleGate.Entered;
        await Fail(breaker, 1);
        clock.Advance(Period);
        var probe = breaker.Apply<object>(async _ =>
        {
            await probeGate.Wait().Guard();
            throw probeError;
        })(default).AsTask();
        await probeGate.Entered;
        staleGate.Release();
        // Assert
        if (staleFailure)
        {
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await stale.Guard()));
        }
        else
        {
            Assert.Same(Sentinel, await stale.Guard());
        }
        Assert.False(probe.IsCompleted);
        await Rejected(breaker);
        // Act
        clock.Advance(TimeSpan.FromSeconds(3));
        probeGate.Release();
        // Assert
        Assert.Same(probeError, await Assert.ThrowsAsync<InvalidOperationException>(async () => await probe.Guard()));
        await Rejected(breaker);
        // Act
        clock.Advance(Period - TimeSpan.FromTicks(1));
        // Assert
        await Rejected(breaker);
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    [Fact]
    public async Task Apply_StaleSuccessCannotResetNewGenerationFailureCount()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock);
        using var staleGate = new AsyncGate();
        using var probeGate = new AsyncGate();
        // Act
        var stale = breaker.Apply<object>(staleGate.Result)(default).AsTask();
        await staleGate.Entered;
        await Fail(breaker);
        clock.Advance(Period);
        var probe = breaker.Apply<object>(probeGate.Result)(default).AsTask();
        await probeGate.Entered;
        probeGate.Release();
        // Assert
        Assert.Same(Sentinel, await probe.Guard());
        // Act
        await Fail(breaker, 1);
        staleGate.Release();
        // Assert
        Assert.Same(Sentinel, await stale.Guard());
        // Act
        await Fail(breaker, 1);
        // Assert
        await Rejected(breaker);
    }

    [Fact]
    public async Task Apply_StaleFailureCannotIncrementNewGenerationFailureCount()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock);
        using var staleGate = new AsyncGate();
        using var probeGate = new AsyncGate();
        var expected = new InvalidOperationException("old failure");
        // Act
        var stale = breaker.Apply<object>(async _ =>
        {
            await staleGate.Wait().Guard();
            throw expected;
        })(default).AsTask();
        await staleGate.Entered;
        await Fail(breaker);
        clock.Advance(Period);
        var probe = breaker.Apply<object>(probeGate.Result)(default).AsTask();
        await probeGate.Entered;
        probeGate.Release();
        // Assert
        Assert.Same(Sentinel, await probe.Guard());
        // Act
        staleGate.Release();
        // Assert
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await stale.Guard()));
        // Act
        await Fail(breaker, 1);
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }

    [Fact]
    public async Task Apply_StaleFailureCannotRestartExistingOpenInterval()
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        var breaker = Breaker(clock, 1);
        using var staleGate = new AsyncGate();
        var expected = new InvalidOperationException("old failure");
        // Act
        var stale = breaker.Apply<object>(async _ =>
        {
            await staleGate.Wait().Guard();
            throw expected;
        })(default).AsTask();
        await staleGate.Entered;
        await Fail(breaker, 1);
        clock.Advance(TimeSpan.FromSeconds(6));
        staleGate.Release();
        // Assert
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await stale.Guard()));
        // Act
        clock.Advance(TimeSpan.FromSeconds(4) - TimeSpan.FromTicks(1));
        // Assert
        await Rejected(breaker);
        // Act
        clock.Advance(TimeSpan.FromTicks(1));
        // Assert
        Assert.Same(Sentinel, await breaker.Apply(Success())(default).Guard());
    }
}
