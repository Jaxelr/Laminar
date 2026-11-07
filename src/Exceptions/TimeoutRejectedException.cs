namespace Laminar;

/// <summary>
/// Represents an operation that exceeded its cooperative timeout without caller cancellation.
/// </summary>
public sealed class TimeoutRejectedException : ResilienceRejectedException
{
    /// <summary>
    /// Initializes a timeout rejection with its configured duration and an optional underlying exception.
    /// </summary>
    public TimeoutRejectedException(TimeSpan timeout, Exception? innerException = null)
        : base($"The operation exceeded its timeout of {timeout}.", innerException)
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Gets the configured timeout duration that the operation exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }
}
