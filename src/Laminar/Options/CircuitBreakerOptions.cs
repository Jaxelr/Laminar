namespace Laminar;

/// <summary>
/// Configures the circuit breaker's failure threshold, break duration, failure predicate, and time source.
/// </summary>
public sealed record CircuitBreakerOptions
{
    /// <summary>
    /// Gets the consecutive handled failure count that opens the circuit. Defaults to five.
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// Gets the time the circuit remains open before allowing a probe. Defaults to thirty seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the failure predicate. By default, cancellation and resilience rejection are excluded.
    /// </summary>
    public Func<Exception, bool> ShouldHandle { get; init; } = FailureHandling.IsHandled;

    /// <summary>
    /// Gets the time source used to measure break durations. Defaults to the system time provider.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
