namespace Laminar;

public sealed class TimeoutResilience : IOperationResilience
{
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    public TimeoutResilience(TimeSpan timeout, TimeProvider? timeProvider = null)
    {
        FailureHandling.ValidateTimer(timeout, nameof(timeout));
        this.timeout = timeout;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Func<CancellationToken, ValueTask<TResult>> Apply<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken => ExecuteAsync(operation, cancellationToken);
    }

    private async ValueTask<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = new(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var result = await operation(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException exception) when (deadline.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutRejectedException(timeout, exception);
        }
    }
}
