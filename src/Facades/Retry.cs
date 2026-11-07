namespace Laminar;

public static class Retry
{
    public static ValueTask Retryable(
        Func<Task> operation,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.Retryable(operation, options)(cancellationToken);

    public static ValueTask<TResult> Retryable<TResult>(
        Func<Task<TResult>> operation,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.Retryable(operation, options)(cancellationToken);

    public static ValueTask Retryable(
        Func<CancellationToken, Task> operation,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.Retryable(operation, options)(cancellationToken);

    public static ValueTask<TResult> Retryable<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.Retryable(operation, options)(cancellationToken);
}
