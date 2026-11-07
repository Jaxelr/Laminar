namespace Laminar;

public static class CircuitBreaker
{
    public static ValueTask CircuitBrokenBy(
        Func<Task> operation,
        CircuitBreakerResilience circuitBreaker,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.CircuitBrokenBy(operation, circuitBreaker)(cancellationToken);

    public static ValueTask<TResult> CircuitBrokenBy<TResult>(
        Func<Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.CircuitBrokenBy(operation, circuitBreaker)(cancellationToken);

    public static ValueTask CircuitBrokenBy(
        Func<CancellationToken, Task> operation,
        CircuitBreakerResilience circuitBreaker,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.CircuitBrokenBy(operation, circuitBreaker)(cancellationToken);

    public static ValueTask<TResult> CircuitBrokenBy<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CircuitBreakerResilience circuitBreaker,
        CancellationToken cancellationToken = default)
        => ResilienceExtensions.CircuitBrokenBy(operation, circuitBreaker)(cancellationToken);
}
