namespace Laminar;

/// <summary>
/// Represents rejection by an open circuit breaker, including while another probe is in progress.
/// </summary>
public sealed class CircuitOpenException : ResilienceRejectedException
{
    /// <summary>
    /// Initializes a circuit-open rejection with the default message.
    /// </summary>
    public CircuitOpenException()
        : base("The circuit breaker is open.")
    {
    }
}
