namespace Laminar;

public sealed class RateLimitRejectedException : ResilienceRejectedException
{
    public RateLimitRejectedException(TimeSpan? retryAfter = null)
        : base("The rate limiter rejected the operation.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
