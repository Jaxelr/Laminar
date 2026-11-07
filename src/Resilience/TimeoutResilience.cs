namespace Laminar;

/// <summary>
/// Applies cooperative cancellation at a deadline, waits for completion, and rejects late results.
/// </summary>
public sealed class TimeoutResilience : IOperationResilience
{
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a cooperative timeout, using the system time provider when none is supplied.
    /// </summary>
    public TimeoutResilience(TimeSpan timeout, TimeProvider? timeProvider = null)
    {
        FailureHandling.ValidateTimer(timeout, nameof(timeout));
        this.timeout = timeout;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Returns an operation with a linked deadline token, preserving caller cancellation separately from timeout rejection.
    /// </summary>
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
