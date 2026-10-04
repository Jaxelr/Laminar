namespace Laminar;

/// <summary>
/// Represents a rate limiter's refusal to grant the permits required for an operation.
/// </summary>
public sealed class RateLimitRejectedException : ResilienceRejectedException
{
    /// <summary>
    /// Initializes a rate-limit rejection with an optional limiter-provided retry delay.
    /// </summary>
    public RateLimitRejectedException(TimeSpan? retryAfter = null)
        : base("The rate limiter rejected the operation.")
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the limiter-provided retry delay, or null when that metadata is unavailable.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
