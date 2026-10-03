namespace Laminar;

internal static class FailureHandling
{
    internal static bool IsHandled(Exception exception)
        => exception is not OperationCanceledException and not ResilienceRejectedException;

    internal static void ValidateTimer(TimeSpan duration, string parameterName, bool allowZero = false)
    {
        if (duration < TimeSpan.Zero || (!allowZero && duration == TimeSpan.Zero)
            || duration.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
