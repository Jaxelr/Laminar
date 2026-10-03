namespace Laminar;

public static class OperationExtensions
{
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        IOperationResilience resilience)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(resilience);
        return resilience.Apply(operation);
    }

    public static Func<CancellationToken, ValueTask> ResilientWith(
        this Func<CancellationToken, ValueTask> operation,
        IOperationResilience resilience)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(resilience);
        async ValueTask<bool> withResult(CancellationToken cancellationToken)
        {
            await operation(cancellationToken).ConfigureAwait(false);
            return true;
        }
        var decorated = resilience.Apply(withResult);
        return async cancellationToken => _ = await decorated(cancellationToken).ConfigureAwait(false);
    }

    public static Func<CancellationToken, ValueTask<TResult>> AsOperation<TResult>(
        this Func<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operation());
        };
    }

    public static Func<CancellationToken, ValueTask> AsOperation(this Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            operation();
            return ValueTask.CompletedTask;
        };
    }

    public static Func<CancellationToken, ValueTask<TResult>> AsOperation<TResult>(
        this Func<CancellationToken, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<TResult>(operation(cancellationToken));
        };
    }

    public static Func<CancellationToken, ValueTask> AsOperation(
        this Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask(operation(cancellationToken));
        };
    }

    public static Func<CancellationToken, ValueTask<TResult>> AsOperation<TResult>(
        this Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Func<CancellationToken, Task<TResult>> cancellable = _ => operation();
        return cancellable.AsOperation();
    }

    public static Func<CancellationToken, ValueTask> AsOperation(this Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Func<CancellationToken, Task> cancellable = _ => operation();
        return cancellable.AsOperation();
    }
}
