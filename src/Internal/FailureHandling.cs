namespace Laminar;

/// <summary>
/// Provides default failure classification and timer validation for resilience patterns.
/// </summary>
internal static class FailureHandling
{
    /// <summary>
    /// Identifies failures other than cancellation and resilience rejection.
    /// </summary>
    internal static bool IsHandled(Exception exception)
        => exception is not OperationCanceledException and not ResilienceRejectedException;

    /// <summary>
    /// Rejects durations outside the supported timer range, optionally accepting zero.
    /// </summary>
    internal static void ValidateTimer(TimeSpan duration, string parameterName, bool allowZero = false)
    {
        if (duration < TimeSpan.Zero || (!allowZero && duration == TimeSpan.Zero)
            || duration.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
