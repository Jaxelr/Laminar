namespace Laminar.Tests.Helpers;

internal sealed class AsyncGate : IDisposable
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Entered => entered.Task.Guard();
    internal void Release() => release.TrySetResult();

    internal async ValueTask Wait()
    {
        entered.TrySetResult();
        await release.Task.Guard();
    }

    internal async ValueTask<object> Result(CancellationToken _)
    {
        await Wait().Guard();
        return Sentinel;
    }

    public void Dispose() => Release();
}
