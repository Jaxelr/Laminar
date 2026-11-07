namespace Laminar.Tests;

public sealed class TimeoutCleanupTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Apply_CompletionDisposesDeadlineAndDetachesCallerWithoutLateEffects(bool fail, bool cancelCallerFirst)
    {
        // Arrange
        var clock = new RecordingTimeProvider();
        using var caller = new CancellationTokenSource();
        CancellationToken captured = default;
        CancellationTokenRegistration observer = default;
        int cancellations = 0;
        int calls = 0;
        var expected = new InvalidOperationException("completed fault");
        var operation = new TimeoutResilience(Period, clock).Apply<object>(token =>
        {
            calls++;
            captured = token;
            observer = token.Register(() => Interlocked.Increment(ref cancellations));
            if (fail)
            {
                throw expected;
            }
            return ValueTask.FromResult(Sentinel);
        });
        try
        {
            // Act
            // Assert
            if (fail)
            {
                Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await operation(caller.Token).Guard()));
            }
            else
            {
                Assert.Same(Sentinel, await operation(caller.Token).Guard());
            }
            Assert.Equal(1, calls);
            Assert.Equal(1, clock.Created);
            Assert.Equal(1, clock.Disposed);
            // Act
            if (cancelCallerFirst)
            {
                caller.Cancel();
            }
            clock.Advance(Period + Period);
            if (!cancelCallerFirst)
            {
                caller.Cancel();
            }
            // Assert
            Assert.False(captured.IsCancellationRequested);
            Assert.Equal(0, cancellations);
            Assert.Equal(1, calls);
        }
        finally
        {
            observer.Dispose();
        }
    }
}
