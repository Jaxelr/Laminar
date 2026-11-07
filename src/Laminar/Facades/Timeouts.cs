namespace Laminar;

public static class Timeouts
{
    public static ValueTask TimedOutAfter(
        Func<Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.TimedOutAfter(operation, timeout, timeProvider)(cancellationToken);

    public static ValueTask<TResult> TimedOutAfter<TResult>(
        Func<Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.TimedOutAfter(operation, timeout, timeProvider)(cancellationToken);

    public static ValueTask TimedOutAfter(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.TimedOutAfter(operation, timeout, timeProvider)(cancellationToken);

    public static ValueTask<TResult> TimedOutAfter<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        TimeSpan timeout,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.TimedOutAfter(operation, timeout, timeProvider)(cancellationToken);
}
