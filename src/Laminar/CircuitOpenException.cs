namespace Laminar;

public sealed class CircuitOpenException : ResilienceRejectedException
{
    public CircuitOpenException()
        : base("The circuit breaker is open.")
    {
    }
}
