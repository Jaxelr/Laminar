namespace Laminar;

public sealed record CircuitBreakerOptions
{
    public int FailureThreshold { get; init; } = 5;

    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    public Func<Exception, bool> ShouldHandle { get; init; } = FailureHandling.IsHandled;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
