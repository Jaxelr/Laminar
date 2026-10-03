namespace Laminar.Tests;

public sealed class RetryResilienceTests
{
    [Fact]
    public async Task Apply_ImmediateSuccessUsesOneAttempt()
    {
        // Arrange
        int calls = 0;
        var operation = new RetryResilience(new()).Apply(Success(() => calls++));
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Apply_TwoHandledFailuresThenSuccessUsesThreeAttempts()
    {
        // Arrange
        int calls = 0;
        var operation = new RetryResilience(new() { MaxRetries = 2 }).Apply<object>(_ =>
        {
            if (++calls <= 2)
            {
                throw new InvalidOperationException("transient");
            }
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    public async Task Apply_RetryBudgetBoundsAttemptCount(int retries, int attempts)
    {
        // Arrange
        int calls = 0;
        var expected = new InvalidOperationException("exhausted");
        var operation = new RetryResilience(new() { MaxRetries = retries }).Apply<object>(_ =>
        {
            calls++;
            throw expected;
        });
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Equal(attempts, calls);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Apply_ExhaustionPreservesFinalExceptionIdentity()
    {
        // Arrange
        var errors = new[] { new InvalidOperationException("first"), new InvalidOperationException("second"),
            new InvalidOperationException("last") };
        int calls = 0;
        var operation = new RetryResilience(new() { MaxRetries = 2 }).Apply<object>(_ => throw errors[calls++]);
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Same(errors[2], actual);
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_CustomPredicateControlsRetryAndReceivesOriginalFailure(bool accepted)
    {
        // Arrange
        var expected = new InvalidOperationException("selected");
        var observed = new List<Exception>();
        int calls = 0;
        var operation = new RetryResilience(new()
        {
            ShouldHandle = exception =>
            {
                observed.Add(exception);
                return accepted && exception == expected;
            },
        }).Apply<object>(_ =>
        {
            if (++calls == 1)
            {
                throw expected;
            }
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        if (accepted)
        {
            Assert.Same(Sentinel, await operation(default).Guard());
        }
        else
        {
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await operation(default).Guard()));
        }
        Assert.Equal(accepted ? 2 : 1, calls);
        Assert.Same(expected, Assert.Single(observed));
    }

    [Fact]
    public async Task Apply_CancellationDuringFailurePreventsRetry()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        int calls = 0;
        int predicates = 0;
        var expected = new InvalidOperationException("after cancel");
        var operation = new RetryResilience(new()
        {
            ShouldHandle = _ =>
            {
                predicates++;
                return true;
            },
        }).Apply<object>(_ =>
        {
            calls++;
            caller.Cancel();
            throw expected;
        });
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(caller.Token).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
        Assert.Equal(0, predicates);
        Assert.True(caller.IsCancellationRequested);
    }

    [Fact]
    public async Task Apply_SequentialInvocationsHaveIndependentBudgets()
    {
        // Arrange
        int calls = 0;
        var operation = new RetryResilience(new() { MaxRetries = 2 }).Apply<object>(_ =>
        {
            if (++calls % 3 != 0)
            {
                throw new InvalidOperationException("transient");
            }
            return ValueTask.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(3, calls);
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(6, calls);
    }

    [Fact]
    public async Task Apply_ConcurrentInvocationsHaveIndependentBudgets()
    {
        // Arrange
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        using var firstGate = new AsyncGate();
        using var secondGate = new AsyncGate();
        int[] attempts = [0, 0];
        object[] results = [new(), new()];
        var operation = new RetryResilience(new() { MaxRetries = 2 }).Apply<object>(async token =>
        {
            int id = token == first.Token ? 0 : 1;
            int attempt = Interlocked.Increment(ref attempts[id]);
            if (attempt == 1)
            {
                await (id == 0 ? firstGate : secondGate).Wait().Guard();
            }
            if (attempt <= 2)
            {
                throw new InvalidOperationException($"call-{id}");
            }
            return results[id];
        });
        // Act
        var a = operation(first.Token).AsTask();
        var b = operation(second.Token).AsTask();
        await Task.WhenAll(firstGate.Entered, secondGate.Entered).Guard();
        // Assert
        Assert.Equal([1, 1], attempts);
        // Act
        firstGate.Release();
        // Assert
        Assert.Same(results[0], await a.Guard());
        Assert.False(b.IsCompleted);
        // Act
        secondGate.Release();
        // Assert
        Assert.Same(results[1], await b.Guard());
        Assert.Equal([3, 3], attempts);
    }

    [Fact]
    public async Task Apply_PreCanceledCallerSkipsAllAttempts()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;
        var operation = new RetryResilience(new()).Apply(Success(() => calls++));
        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation(caller.Token).Guard());
        Assert.Equal(0, calls);
    }
}
