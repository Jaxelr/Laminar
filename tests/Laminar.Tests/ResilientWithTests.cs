namespace Laminar.Tests;

public sealed class ResilientWithTests
{
    [Fact]
    public async Task ResilientWith_ResultAppliesAtWrappingAndUsesWrapper()
    {
        // Arrange
        var log = new List<string>();
        var policy = new RecordingResilience("policy", log);
        using var caller = new CancellationTokenSource();
        int calls = 0;
        // Act
        var operation = Success(() => calls++).ResilientWith(policy);
        // Assert
        Assert.Equal(1, policy.Applies);
        Assert.Equal(typeof(object), policy.ResultType);
        Assert.Equal(0, calls);
        Assert.Empty(log);
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(caller.Token).Guard());
        Assert.Equal(caller.Token, policy.Token);
        Assert.Same(Sentinel, policy.Result);
        Assert.Equal(1, calls);
        Assert.Equal(["policy-enter", "policy-exit"], log);
    }

    [Fact]
    public async Task ResilientWith_VoidBridgesTrueAfterCompletion()
    {
        // Arrange
        using var gate = new AsyncGate();
        var log = new List<string>();
        var policy = new RecordingResilience("policy", log);
        Func<CancellationToken, ValueTask> callback = _ => gate.Wait();
        // Act
        var operation = callback.ResilientWith(policy);
        // Assert
        Assert.Equal(1, policy.Applies);
        Assert.Equal(typeof(bool), policy.ResultType);
        // Act
        var pending = operation(default).AsTask();
        await gate.Entered;
        // Assert
        Assert.False(pending.IsCompleted);
        Assert.Null(policy.Result);
        Assert.Equal(["policy-enter"], log);
        // Act
        gate.Release();
        await pending.Guard();
        // Assert
        Assert.Equal(true, policy.Result);
        Assert.Equal(["policy-enter", "policy-exit"], log);
    }

    [Fact]
    public async Task ResilientWith_NestingPreservesOuterInnerOrder()
    {
        // Arrange
        var log = new List<string>();
        var inner = new RecordingResilience("inner", log);
        var outer = new RecordingResilience("outer", log);
        var operation = Success(() => log.Add("callback")).ResilientWith(inner).ResilientWith(outer);
        // Act
        // Assert
        Assert.Same(Sentinel, await operation(default).Guard());
        Assert.Equal(["outer-enter", "inner-enter", "callback", "inner-exit", "outer-exit"], log);
        Assert.Equal(1, inner.Applies);
        Assert.Equal(1, outer.Applies);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResilientWith_NullArgumentsThrowImmediately(bool isVoid, bool nullPolicy)
    {
        // Arrange
        IOperationResilience policy = nullPolicy ? null! : new RecordingResilience("policy", []);
        var result = nullPolicy ? Success() : null!;
        Func<CancellationToken, ValueTask> action = nullPolicy ? _ => ValueTask.CompletedTask : null!;
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (isVoid)
            {
                _ = action.ResilientWith(policy);
            }
            else
            {
                _ = result.ResilientWith(policy);
            }
        });
        // Assert
        Assert.Equal(nullPolicy ? "resilience" : "operation", exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResilientWith_PreservesFaultIdentityAndExactCallerToken(bool isVoid)
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        var log = new List<string>();
        var policy = new RecordingResilience("policy", log);
        var expected = new InvalidOperationException("wrapped fault");
        CancellationToken forwarded = default;
        int calls = 0;
        Func<CancellationToken, ValueTask<object>> result = token =>
        {
            calls++;
            forwarded = token;
            throw expected;
        };
        Func<CancellationToken, ValueTask> action = token =>
        {
            calls++;
            forwarded = token;
            throw expected;
        };
        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (isVoid)
            {
                await action.ResilientWith(policy)(caller.Token).Guard();
            }
            else
            {
                await result.ResilientWith(policy)(caller.Token).Guard();
            }
        });
        // Assert
        Assert.Same(expected, actual);
        Assert.Equal(caller.Token, forwarded);
        Assert.Equal(caller.Token, policy.Token);
        Assert.Equal(isVoid ? typeof(bool) : typeof(object), policy.ResultType);
        Assert.Equal(1, policy.Applies);
        Assert.Equal(1, calls);
        Assert.Equal(["policy-enter"], log);
        Assert.Null(policy.Result);
    }
}
