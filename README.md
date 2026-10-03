# Laminar

Small, operation-first resilience for .NET 10. Decorate delegates with rate
limiting, retry, cooperative timeout, circuit breaking, or your own pattern.
No HTTP dependency, pipelines, or registries.

## Usage

```csharp
using Laminar;
using System.Threading.RateLimiting;

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
var limited = hello.AsOperation().RateLimitedBy(limiter);

string result = await limited(CancellationToken.None);
```

`AsOperation()` adapts `Action`, `Func<T>`, and `Task`-returning delegates,
including those accepting a cancellation token. The common contract is
`Func<CancellationToken, ValueTask<T>>` (or `ValueTask` without a result).
Delegates with other arguments can be bound through a lambda.

## Patterns

```csharp
Func<CancellationToken, ValueTask<string>> operation =
    cancellationToken => ValueTask.FromResult("Hello, world!");

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
| `Retryable` | Defaults to 3 retries with no delay. Cancellation and resilience rejections are excluded by default; customize `ShouldHandle` to select failures. |
| `TimedOutAfter` | Cancels the operation's token at the deadline and throws `TimeoutRejectedException`. Caller cancellation remains cancellation. |
| `CircuitBrokenBy` | Opens after consecutive handled failures, throws `CircuitOpenException` while open, and admits one probe after the break duration. A successful probe closes it. |

The last decorator is outermost: `operation.RateLimitedBy(limiter).Retryable()`
acquires a permit for every attempt; `operation.Retryable().RateLimitedBy(limiter)`
acquires once for the whole retry sequence.

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

## Development

```text
dotnet build Laminar.slnx --configuration Release --warnaserror
dotnet test --solution Laminar.slnx --configuration Release --no-build
dotnet format Laminar.slnx --verify-no-changes --severity info
```
