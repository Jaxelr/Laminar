# Laminar

Small, operation-first resilience for .NET 10. Decorate delegates with rate
limiting, retry, cooperative timeout, circuit breaking, or your own pattern.
No HTTP dependency, pipelines, or registries.

## Usage

```csharp
using System.Threading.RateLimiting;
using Laminar;

using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 10,
    TokensPerPeriod = 2,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    AutoReplenishment = true,
    QueueLimit = 5,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
});

Func<string> hello = () => "Hello, world!";
var limited = hello.RateLimitedBy(limiter);

string result = await limited(CancellationToken.None);
```

Every pattern adapts `Action`, `Func<T>`, and `Task`- or `ValueTask`-returning
delegates automatically. Async delegates may accept a cancellation token.
Invoke the returned delegate with `await guarded(cancellationToken)`.
Delegates with other arguments can be bound through a lambda.

For immediate, one-line execution with an existing resilience pattern, use
`ExecuteAsync`:

```csharp
var retry = new RetryResilience(new RetryOptions
{
    MaxRetries = 3,
    Delay = TimeSpan.FromSeconds(2),
    BackoffFactor = 2
});

string result = await retry.ExecuteAsync(
    () => Task.FromResult("Hello, world!"));
```

`ExecuteAsync` accepts `Task`- and `Task<T>`-returning delegates, with or without
a cancellation-token parameter. An optional caller token is passed as the second
argument and forwarded to cancellation-aware delegates. The helpers return
`ValueTask` or `ValueTask<T>` and use the same adapters and resilience behavior as
`ResilientWith`. For reusable decorated delegates, keep using `ResilientWith`.

## Patterns

```csharp
Func<CancellationToken, Task<string>> operation =
    cancellationToken => Task.FromResult("Hello, world!");

var breaker = new CircuitBreakerResilience(new CircuitBreakerOptions
{
    FailureThreshold = 5,
    BreakDuration = TimeSpan.FromSeconds(30)
});

var resilient = operation
    .RateLimitedBy(limiter)
    .Retryable(new RetryOptions
    {
        MaxRetries = 3,
        Delay = TimeSpan.FromMilliseconds(100),
        ShouldHandle = exception => exception is IOException
    })
    .TimedOutAfter(TimeSpan.FromSeconds(5))
    .CircuitBrokenBy(breaker);

string result = await resilient(cancellationToken);
```

| Pattern | Behavior |
|---|---|
| `RateLimitedBy` | Uses any .NET `RateLimiter`; queues according to its options, then throws `RateLimitRejectedException` on rejection. Optional `permitCount` defaults to 1. |
| `Retryable` | Defaults to 3 retries with no delay. Set `Delay` for fixed delays, or also set `BackoffFactor` for exponential backoff. Cancellation and resilience rejections are excluded by default; customize `ShouldHandle` to select failures. |
| `TimedOutAfter` | Cancels the operation's token at the deadline and throws `TimeoutRejectedException`. Caller cancellation remains cancellation. |
| `CircuitBrokenBy` | Opens after consecutive handled failures, throws `CircuitOpenException` while open, and admits one probe after the break duration. A successful probe closes it. |

The last decorator is outermost: `operation.RateLimitedBy(limiter).Retryable()`
acquires a permit for every attempt; `operation.Retryable().RateLimitedBy(limiter)`
acquires once for the whole retry sequence.

Retry delays start at `Delay` and are multiplied by `BackoffFactor` for each
subsequent retry. The factor defaults to 1 (fixed delay) and must be finite and at
least 1. For example, this retries after 2, 4, and 8 seconds:

```csharp
var retried = operation.Retryable(new RetryOptions
{
    MaxRetries = 3,
    Delay = TimeSpan.FromSeconds(2),
    BackoffFactor = 2
});
```

Growing delays are capped at .NET's maximum timer duration (4,294,967,294
milliseconds). A zero delay stays immediate regardless of the factor. Every
invocation starts its own backoff sequence, and delays honor caller cancellation.

Reuse a limiter or breaker to share its state across operations. You own the
limiter's lifetime. Limits are in-process. Retry only operations safe to repeat.
Timeout is cooperative: code that ignores cancellation cannot be forcibly stopped;
the wrapper waits for completion and rejects a late result. Synchronous adapters
run inline, and delegates without a token cannot observe cancellation while running.

## Custom patterns

Implement `IOperationResilience` and apply it with `ResilientWith`.

```csharp
public sealed class CustomResilience : IOperationResilience
{
    public Func<CancellationToken, ValueTask<T>> Apply<T>(
        Func<CancellationToken, ValueTask<T>> operation)
        => cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operation(cancellationToken);
        };
}

var decorated = operation.ResilientWith(new CustomResilience());
```

## Contributing

Issues and pull requests are welcome. For larger changes, open an issue first to
discuss the approach. Keep contributions focused, follow the existing code style,
and include tests and documentation updates for behavior changes. Before submitting
a pull request, ensure the solution builds, tests pass, and formatting is clean.
