using static Laminar.Retry;

namespace Laminar.Tests;

public sealed class RetryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retryable_AsyncResultLambdaInfersTypeAndRetries(bool tokenAware, bool useDefaults)
    {
        // Arrange
        int calls = 0;
        CancellationToken forwarded = new(true);
        int failures = useDefaults ? 3 : 2;
        RetryOptions? options = useDefaults ? null : new() { MaxRetries = failures };
        async Task<object> Callback(CancellationToken token)
        {
            forwarded = token;
            await Task.Yield();
            if (++calls <= failures)
            {
                throw new InvalidOperationException("retry");
            }
            return Sentinel;
        }

        // Act
        object result = tokenAware
            ? await Retryable(async token => await Callback(token), options).Guard()
            : await Retryable(async () => await Callback(default), options).Guard();

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Equal(failures + 1, calls);
        Assert.Equal(CancellationToken.None, forwarded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retryable_AsyncVoidLambdaRetries(bool tokenAware)
    {
        // Arrange
        int calls = 0;
        CancellationToken forwarded = new(true);
        var options = new RetryOptions { MaxRetries = 1 };
        async Task Callback(CancellationToken token)
        {
            forwarded = token;
            await Task.Yield();
            if (++calls == 1)
            {
                throw new InvalidOperationException("retry");
            }
        }

        // Act
        if (tokenAware)
        {
            await Retryable(async token => await Callback(token), options).Guard();
        }
        else
        {
            await Retryable(async () => await Callback(default), options).Guard();
        }

        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(CancellationToken.None, forwarded);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retryable_ResultWaitsForCompletionAndForwardsToken(bool tokenAware, bool withCallerToken)
    {
        // Arrange
        using var gate = new AsyncGate();
        using var caller = new CancellationTokenSource();
        var token = withCallerToken ? caller.Token : default;
        CancellationToken forwarded = default;
        Task<object> Callback(CancellationToken cancellationToken)
        {
            forwarded = cancellationToken;
            return gate.Result(cancellationToken).AsTask();
        }

        // Act
        var pending = tokenAware
            ? Retryable(ct => Callback(ct), cancellationToken: token).AsTask()
            : Retryable(() => Callback(default), cancellationToken: token).AsTask();
        await gate.Entered;

        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Equal(tokenAware ? token : CancellationToken.None, forwarded);

        // Act
        gate.Release();
        object result = await pending.Guard();

        // Assert
        Assert.Same(Sentinel, result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retryable_VoidWaitsForCompletionAndForwardsToken(bool tokenAware, bool withCallerToken)
    {
        // Arrange
        using var gate = new AsyncGate();
        using var caller = new CancellationTokenSource();
        var token = withCallerToken ? caller.Token : default;
        CancellationToken forwarded = default;
        bool completed = false;
        async Task Callback(CancellationToken cancellationToken)
        {
            forwarded = cancellationToken;
            await gate.Wait();
            completed = true;
        }

        // Act
        var pending = tokenAware
            ? Retryable(ct => Callback(ct), cancellationToken: token).AsTask()
            : Retryable(() => Callback(default), cancellationToken: token).AsTask();
        await gate.Entered;

        // Assert
        Assert.False(pending.IsCompleted);
        Assert.False(completed);
        Assert.Equal(tokenAware ? token : CancellationToken.None, forwarded);

        // Act
        gate.Release();
        await pending.Guard();

        // Assert
        Assert.True(completed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retryable_CanceledCallerDoesNotInvokeOperation(bool withResult, bool tokenAware)
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;
        Task<object> Callback()
        {
            calls++;
            return Task.FromResult(Sentinel);
        }

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var pending = (withResult, tokenAware) switch
            {
                (true, true) => Retryable(_ => Callback(), cancellationToken: caller.Token).AsTask(),
                (true, false) => Retryable(() => Callback(), cancellationToken: caller.Token).AsTask(),
                (false, true) => Retryable(_ => (Task) Callback(), cancellationToken: caller.Token).AsTask(),
                (false, false) => Retryable(() => (Task) Callback(), cancellationToken: caller.Token).AsTask()
            };
            await pending.Guard();
        });

        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retryable_DefaultRetryExhaustionPreservesException(bool withResult, bool tokenAware)
    {
        // Arrange
        var expected = new InvalidOperationException("send failed");
        int calls = 0;
        async Task<object> Callback()
        {
            calls++;
            await Task.Yield();
            throw expected;
        }

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var pending = (withResult, tokenAware) switch
            {
                (true, true) => Retryable(_ => Callback()).AsTask(),
                (true, false) => Retryable(() => Callback()).AsTask(),
                (false, true) => Retryable(_ => (Task) Callback()).AsTask(),
                (false, false) => Retryable(() => (Task) Callback()).AsTask()
            };
            await pending.Guard();
        });

        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retryable_NullOperationThrowsImmediately(bool withResult, bool tokenAware)
    {
        // Arrange
        Func<Task> operation = null!;
        Func<Task<object>> resultOperation = null!;
        Func<CancellationToken, Task> tokenOperation = null!;
        Func<CancellationToken, Task<object>> tokenResultOperation = null!;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = (withResult, tokenAware) switch
            {
                (true, true) => Retryable(tokenResultOperation).AsTask(),
                (true, false) => Retryable(resultOperation).AsTask(),
                (false, true) => Retryable(tokenOperation).AsTask(),
                (false, false) => Retryable(operation).AsTask()
            };
        });

        // Assert
        Assert.Equal("operation", exception.ParamName);
    }
}
