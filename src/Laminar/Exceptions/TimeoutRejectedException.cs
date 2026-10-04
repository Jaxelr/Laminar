namespace Laminar;

public sealed class TimeoutRejectedException : ResilienceRejectedException
{
    public TimeoutRejectedException(TimeSpan timeout, Exception? innerException = null)
        : base($"The operation exceeded its timeout of {timeout}.", innerException)
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
