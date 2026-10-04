namespace Laminar;

/// <summary>
/// Retries handled operation failures within a per-invocation budget, with an optional delay.
/// </summary>
public sealed class RetryResilience : IOperationResilience
{
    private readonly RetryOptions options;

    /// <summary>
    /// Initializes retry behavior with the supplied options.
    /// </summary>
    public RetryResilience(RetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetries);
        FailureHandling.ValidateTimer(options.Delay, nameof(options.Delay), allowZero: true);
        ArgumentNullException.ThrowIfNull(options.ShouldHandle);
        ArgumentNullException.ThrowIfNull(options.TimeProvider);
        this.options = options;
    }

    /// <summary>
    /// Returns an operation that retries handled failures and stops retrying when the caller cancels.
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
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                && attempt < options.MaxRetries && options.ShouldHandle(exception))
            {
                if (options.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(options.Delay, options.TimeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
