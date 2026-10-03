namespace Laminar;

public abstract class ResilienceRejectedException : Exception
{
    protected ResilienceRejectedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
