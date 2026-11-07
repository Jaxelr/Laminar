namespace Laminar;

/// <summary>
/// Opens after consecutive handled failures and allows one recovery probe after the break duration.
/// </summary>
public sealed class CircuitBreakerResilience : IOperationResilience
{
    private readonly CircuitBreakerOptions options;
    private readonly Lock gate = new();
    private int failures;
    private bool isOpen;
    private bool probeInProgress;
    private long generation;
    private long openedAt;

    /// <summary>
    /// Initializes a shared circuit breaker, using default options when none are supplied.
    /// </summary>
    public CircuitBreakerResilience(CircuitBreakerOptions? options = null)
    {
        this.options = options ?? new CircuitBreakerOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.options.FailureThreshold);
        FailureHandling.ValidateTimer(this.options.BreakDuration, nameof(options.BreakDuration));
        ArgumentNullException.ThrowIfNull(this.options.ShouldHandle);
        ArgumentNullException.ThrowIfNull(this.options.TimeProvider);
    }

    /// <summary>
    /// Returns an operation guarded by this breaker's shared state, rejecting calls while the circuit denies admission.
    /// </summary>
    public Func<CancellationToken, ValueTask<TResult>> Apply<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return cancellationToken => ExecuteAsync(operation, cancellationToken);
    }

    private async ValueTask<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long execution = Enter();
        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            Succeed(execution);
            return result;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && options.ShouldHandle(exception))
        {
            Fail(execution);
            throw;
        }
        finally
        {
            ReleaseProbe(execution);
        }
    }

    private long Enter()
    {
        lock (gate)
        {
            if (isOpen)
            {
                if (probeInProgress
                    || options.TimeProvider.GetElapsedTime(openedAt) < options.BreakDuration)
                {
                    throw new CircuitOpenException();
                }

                probeInProgress = true;
            }

            return generation;
        }
    }

    private void Succeed(long execution)
    {
        lock (gate)
        {
            if (execution != generation)
            {
                return;
            }

            failures = 0;
            if (isOpen)
            {
                isOpen = false;
                probeInProgress = false;
                generation++;
            }
        }
    }

    private void Fail(long execution)
    {
        lock (gate)
        {
            if (execution != generation)
            {
                return;
            }

            failures++;
            if (isOpen || failures >= options.FailureThreshold)
            {
                isOpen = true;
                probeInProgress = false;
                openedAt = options.TimeProvider.GetTimestamp();
                generation++;
            }
        }
    }

    private void ReleaseProbe(long execution)
    {
        lock (gate)
        {
            if (execution == generation && isOpen)
            {
                probeInProgress = false;
            }
        }
    }
}
