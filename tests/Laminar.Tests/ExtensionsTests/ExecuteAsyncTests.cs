namespace Laminar.Tests;

public sealed class ExecuteAsyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ResultLambdaUsesPolicyAndForwardsToken(bool tokenAware)
    {
        // Arrange
        var log = new List<string>();
        var policy = new RecordingResilience("policy", log);
        using var caller = new CancellationTokenSource();
        CancellationToken forwarded = default;
        int calls = 0;
        Task<object> Callback(CancellationToken token)
        {
            calls++;
            forwarded = token;
            return Task.FromResult(Sentinel);
        }

        // Act
        object result = tokenAware
            ? await policy.ExecuteAsync(token => Callback(token), caller.Token).Guard()
            : await policy.ExecuteAsync(() => Callback(default), caller.Token).Guard();

        // Assert
        Assert.Same(Sentinel, result);
        Assert.Same(Sentinel, policy.Result);
        Assert.Equal(tokenAware ? caller.Token : default, forwarded);
        Assert.Equal(caller.Token, policy.Token);
        Assert.Equal(1, calls);
        Assert.Equal(1, policy.Applies);
        Assert.Equal(["policy-enter", "policy-exit"], log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_AsyncLambdaAwaitsCompletion(bool tokenAware)
    {
        // Arrange
        using var gate = new AsyncGate();
        using var caller = new CancellationTokenSource();
        var policy = new RecordingResilience("policy", []);
        CancellationToken forwarded = default;
        // Act
        var pending = tokenAware
            ? policy.ExecuteAsync(async token =>
            {
                forwarded = token;
                await gate.Wait();
            }, caller.Token).AsTask()
            : policy.ExecuteAsync(async () => await gate.Wait(), caller.Token).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Null(policy.Result);
        Assert.Equal(tokenAware ? caller.Token : default, forwarded);
        Assert.Equal(caller.Token, policy.Token);

        // Act
        gate.Release();
        await pending.Guard();

        // Assert
        Assert.Equal(true, policy.Result);
        Assert.Equal(1, policy.Applies);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultTokenAndMethodGroupWork()
    {
        // Arrange
        var policy = new RecordingResilience("policy", []);
        static Task Callback()
        {
            return Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Callback).Guard();

        // Assert
        Assert.Equal(CancellationToken.None, policy.Token);
        Assert.Equal(true, policy.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_CanceledCallerDoesNotInvokeOperation(bool withResult)
    {
        // Arrange
        var policy = new RecordingResilience("policy", []);
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        int calls = 0;

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (withResult)
            {
                await policy.ExecuteAsync(() => Task.FromResult(++calls), caller.Token).Guard();
            }
            else
            {
                await policy.ExecuteAsync(() =>
                {
                    calls++;
                    return Task.CompletedTask;
                }, caller.Token).Guard();
            }
        });

        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesAwaitedFailuresAndReturnsResult()
    {
        // Arrange
        var policy = new RetryResilience(new RetryOptions { MaxRetries = 2 });
        int calls = 0;

        // Act
        int result = await policy.ExecuteAsync(async () =>
        {
            await Task.Yield();
            if (++calls < 3)
            {
                throw new InvalidOperationException("retry");
            }
            return 42;
        }).Guard();

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExceptionAfterRetryExhaustion()
    {
        // Arrange
        var policy = new RetryResilience(new RetryOptions { MaxRetries = 1 });
        var expected = new InvalidOperationException("send failed");
        int calls = 0;

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync(async () =>
            {
                calls++;
                await Task.Yield();
                throw expected;
            }).Guard());

        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ExecuteAsync_NullArgumentsThrowImmediately(
        bool withResult, bool tokenAware, bool nullPolicy)
    {
        // Arrange
        IOperationResilience policy = nullPolicy ? null! : new RecordingResilience("policy", []);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = ExecuteNull(policy, withResult, tokenAware, !nullPolicy);
        });

        // Assert
        Assert.Equal(nullPolicy ? "resilience" : "operation", exception.ParamName);
    }

    private static Task ExecuteNull(
        IOperationResilience policy, bool withResult, bool tokenAware, bool nullOperation)
        => (withResult, tokenAware) switch
        {
            (true, true) => policy.ExecuteAsync(
                nullOperation ? (Func<CancellationToken, Task<object>>) null! : _ => Task.FromResult(Sentinel)).AsTask(),
            (true, false) => policy.ExecuteAsync(
                nullOperation ? (Func<Task<object>>) null! : () => Task.FromResult(Sentinel)).AsTask(),
            (false, true) => policy.ExecuteAsync(
                nullOperation ? (Func<CancellationToken, Task>) null! : _ => Task.CompletedTask).AsTask(),
            (false, false) => policy.ExecuteAsync(
                nullOperation ? (Func<Task>) null! : () => Task.CompletedTask).AsTask()
        };
}
