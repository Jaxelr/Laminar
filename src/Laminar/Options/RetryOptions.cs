namespace Laminar;

/// <summary>
/// Configures the retry budget, delay, failure predicate, and time source.
/// </summary>
public sealed record RetryOptions
{
    /// <summary>
    /// Gets the maximum number of retries after the initial attempt. Defaults to three.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets the delay before each retry. Defaults to zero.
    /// </summary>
    public TimeSpan Delay { get; init; }

    public double BackoffFactor { get; init; } = 1;

    /// <summary>
    /// Gets the retry predicate. By default, cancellation and resilience rejection are excluded.
    /// </summary>
    public Func<Exception, bool> ShouldHandle { get; init; } = FailureHandling.IsHandled;

    /// <summary>
    /// Gets the time source used for retry delays. Defaults to the system time provider.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
