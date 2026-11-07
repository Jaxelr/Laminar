namespace Laminar;

/// <summary>
/// Defines a resilience pattern that decorates a cancellation-aware asynchronous operation.
/// </summary>
public interface IOperationResilience
{
    /// <summary>
    /// Returns an operation decorated with this resilience pattern.
    /// </summary>
    Func<CancellationToken, ValueTask<TResult>> Apply<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation);
}
