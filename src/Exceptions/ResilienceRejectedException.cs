namespace Laminar;

/// <summary>
/// Represents an operation rejected by a resilience pattern rather than an operation failure.
/// </summary>
public abstract class ResilienceRejectedException : Exception
{
    /// <summary>
    /// Initializes a rejection with a message and an optional underlying exception.
    /// </summary>
    protected ResilienceRejectedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
