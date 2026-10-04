namespace Laminar;

public sealed record RetryOptions
{
    public int MaxRetries { get; init; } = 3;

    public TimeSpan Delay { get; init; }

    public Func<Exception, bool> ShouldHandle { get; init; } = FailureHandling.IsHandled;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
