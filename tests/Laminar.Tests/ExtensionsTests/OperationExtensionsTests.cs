namespace Laminar.Tests;

public sealed class OperationExtensionsTests
{
    public enum Adapter
    {
        Action,
        FuncResult,
        FuncTask,
        FuncTaskResult,
        FuncTokenTask,
        FuncTokenTaskResult,
    }

    [Theory]
    [InlineData(Adapter.Action)]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public void AsOperation_IsLazy(Adapter kind)
    {
        // Arrange
        int calls = 0;
        // Act
        var operation = Adapt(kind, _ =>
        {
            calls++;
            return Task.FromResult(Sentinel);
        });
        // Assert
        Assert.Equal(0, calls);
        Assert.IsType<Func<CancellationToken, ValueTask<object>>>(operation);
    }

    [Theory]
    [InlineData(Adapter.Action)]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_InvokesCallbackExactlyOnce(Adapter kind)
    {
        // Arrange
        int calls = 0;
        object marker = new();
        object? observed = null;
        using var caller = new CancellationTokenSource();
        var operation = Adapt(kind, _ =>
        {
            calls++;
            observed = marker;
            return Task.FromResult(Sentinel);
        });
        // Act
        object result = await operation(caller.Token).Guard();
        // Assert
        Assert.Equal(1, calls);
        Assert.Same(marker, observed);
        Assert.Same(Sentinel, result);
    }

    [Theory]
    [InlineData(Adapter.Action)]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_PreCanceledTokenSkipsCallback(Adapter kind)
    {
        // Arrange
        int calls = 0;
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        var operation = Adapt(kind, _ =>
        {
            calls++;
            return Task.FromResult(Sentinel);
        });
        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await operation(caller.Token).Guard());
        // Assert
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(Adapter.Action)]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_SynchronousThrowPreservesIdentity(Adapter kind)
    {
        // Arrange
        var expected = new InvalidOperationException("callback");
        int calls = 0;
        var operation = Adapt(kind, _ =>
        {
            calls++;
            throw expected;
        });
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_FaultedTaskPreservesIdentity(Adapter kind)
    {
        // Arrange
        var expected = new InvalidOperationException("task");
        int calls = 0;
        var operation = Adapt(kind, _ =>
        {
            calls++;
            return Task.FromException<object>(expected);
        });
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await operation(default).Guard());
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(Adapter.Action)]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public void AsOperation_NullDelegateThrowsAtWrapping(Adapter kind)
    {
        // Arrange
        // Act
        var actual = Assert.Throws<ArgumentNullException>(() => NullAdapter(kind));
        // Assert
        Assert.Equal("operation", actual.ParamName);
    }

    [Theory]
    [InlineData(Adapter.FuncTask)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_WaitsForUnderlyingTask(Adapter kind)
    {
        // Arrange
        using var gate = new AsyncGate();
        int calls = 0;
        var operation = Adapt(kind, async _ =>
        {
            calls++;
            await gate.Wait().Guard();
            return Sentinel;
        });
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        // Act
        gate.Release();
        // Assert
        Assert.Same(Sentinel, await pending.Guard());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(Adapter.FuncResult)]
    [InlineData(Adapter.FuncTaskResult)]
    [InlineData(Adapter.FuncTokenTask)]
    [InlineData(Adapter.FuncTokenTaskResult)]
    public async Task AsOperation_ForwardsExactTokenAndResult(Adapter kind)
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        CancellationToken observed = default;
        int calls = 0;
        var operation = Adapt(kind, token =>
        {
            calls++;
            observed = token;
            return Task.FromResult(Sentinel);
        });
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(caller.Token).Guard());
        Assert.Equal(kind >= Adapter.FuncTokenTask ? caller.Token : default, observed);
        Assert.Equal(1, calls);
    }

    private static Func<CancellationToken, ValueTask<object>> Adapt(
        Adapter kind, Func<CancellationToken, Task<object>> callback)
        => kind switch
        {
            Adapter.Action => Normalize(((Action) (() => callback(default).GetAwaiter().GetResult())).AsOperation()),
            Adapter.FuncResult => ((Func<object>) (() => callback(default).GetAwaiter().GetResult())).AsOperation(),
            Adapter.FuncTask => Normalize(((Func<Task>) (() => callback(default))).AsOperation()),
            Adapter.FuncTaskResult => ((Func<Task<object>>) (() => callback(default))).AsOperation(),
            Adapter.FuncTokenTask => Normalize(((Func<CancellationToken, Task>) (token => callback(token))).AsOperation()),
            Adapter.FuncTokenTaskResult => callback.AsOperation(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static Func<CancellationToken, ValueTask<object>> Normalize(Func<CancellationToken, ValueTask> operation)
        => async token =>
        {
            await operation(token);
            return Sentinel;
        };

    private static void NullAdapter(Adapter kind) => _ = kind switch
    {
        Adapter.Action => (object) ((Action) null!).AsOperation(),
        Adapter.FuncResult => ((Func<object>) null!).AsOperation(),
        Adapter.FuncTask => ((Func<Task>) null!).AsOperation(),
        Adapter.FuncTaskResult => ((Func<Task<object>>) null!).AsOperation(),
        Adapter.FuncTokenTask => ((Func<CancellationToken, Task>) null!).AsOperation(),
        Adapter.FuncTokenTaskResult => ((Func<CancellationToken, Task<object>>) null!).AsOperation(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
