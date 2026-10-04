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

Source files under `src\Laminar` are grouped into `Exceptions`, `Options`,
`Extensions`, and `Resilience` directories. The `IOperationResilience` contract and
shared `FailureHandling` helper remain at the project root. All types retain the
`Laminar` namespace.

Tests under `tests\Laminar.Tests` mirror these groups in `ExceptionsTests`,
`OptionsTests`, `ExtensionsTests`, and `ResilienceTests` directories.
`DefaultFailureHandlingTests` remains at the test project root, with reusable helpers
in `Helpers`. Test namespaces are unchanged. Shared test imports are declared as `Using` items in
`Laminar.Tests.csproj`, including the static `TestSupport` import.

```text
dotnet build Laminar.slnx --configuration Release --warnaserror
dotnet test --solution Laminar.slnx --configuration Release --no-build
dotnet format Laminar.slnx --verify-no-changes --severity info
```
