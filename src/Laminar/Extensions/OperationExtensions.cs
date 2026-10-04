namespace Laminar;

/// <summary>
/// Adapts delegates to cancellation-aware operations and decorates them with custom resilience patterns.
/// </summary>
public static class OperationExtensions
{
    /// <summary>
    /// Adapts a synchronous result delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<TResult> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a synchronous action and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask> ResilientWith(
        this Action operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a task-returning result delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<Task<TResult>> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a task-returning delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask> ResilientWith(
        this Func<Task> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a cancellation-aware task-returning result delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<CancellationToken, Task<TResult>> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a cancellation-aware task-returning delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask> ResilientWith(
        this Func<CancellationToken, Task> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a value-task-returning result delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<ValueTask<TResult>> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Adapts a value-task-returning delegate and applies the supplied resilience pattern.
    /// </summary>
    public static Func<CancellationToken, ValueTask> ResilientWith(
        this Func<ValueTask> operation,
        IOperationResilience resilience)
        => operation.AsOperation().ResilientWith(resilience);

    /// <summary>
    /// Applies the supplied resilience pattern to a cancellation-aware result operation.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> ResilientWith<TResult>(
        this Func<CancellationToken, ValueTask<TResult>> operation,
        IOperationResilience resilience)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(resilience);
        return resilience.Apply(operation);
    }

    /// <summary>
    /// Applies the supplied resilience pattern to a cancellation-aware operation, awaiting its full completion.
    /// </summary>
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

    /// <summary>
    /// Adapts a synchronous result delegate, running it inline after checking caller cancellation.
    /// </summary>
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

    /// <summary>
    /// Adapts a synchronous action, running it inline after checking caller cancellation.
    /// </summary>
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

    /// <summary>
    /// Adapts a task-returning result delegate, checking and forwarding the caller's cancellation token.
    /// </summary>
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

    /// <summary>
    /// Adapts a task-returning delegate, checking and forwarding the caller's cancellation token.
    /// </summary>
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

    /// <summary>
    /// Adapts a task-returning result delegate, checking cancellation before invocation without forwarding the token.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> AsOperation<TResult>(
        this Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Func<CancellationToken, Task<TResult>> cancellable = _ => operation();
        return cancellable.AsOperation();
    }

    /// <summary>
    /// Adapts a task-returning delegate, checking cancellation before invocation without forwarding the token.
    /// </summary>
    public static Func<CancellationToken, ValueTask> AsOperation(this Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Func<CancellationToken, Task> cancellable = _ => operation();
        return cancellable.AsOperation();
    }

    /// <summary>
    /// Adapts a value-task-returning result delegate, checking cancellation before invocation without forwarding the token.
    /// </summary>
    public static Func<CancellationToken, ValueTask<TResult>> AsOperation<TResult>(
        this Func<ValueTask<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operation();
        };
    }

    /// <summary>
    /// Adapts a value-task-returning delegate, checking cancellation before invocation without forwarding the token.
    /// </summary>
    public static Func<CancellationToken, ValueTask> AsOperation(this Func<ValueTask> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operation();
        };
    }
}
