# Changelog

All releases, including prereleases, are listed below in newest-first order.

## [0.1.3](https://github.com/Jaxelr/Laminar/releases/tag/v0.1.3) - 2026-11-01

### Added

- Immediate-execution facades: `Retry.Retryable`, `Timeouts.TimedOutAfter`,
  `RateLimit.RateLimitedBy`, and `CircuitBreaker.CircuitBrokenBy`.
- Four overloads per facade for `Task`- and `Task<T>`-returning delegates, with or
  without a cancellation-token parameter. Inline async lambdas infer result types
  without an explicit delegate declaration.
- Static imports for concise calls such as
  `await Retryable(() => client.SendEmailAsync(message))`.

The facades return `ValueTask` or `ValueTask<T>` and reuse the existing adapters
and resilience engine. Timeouts remain cooperative and accept an optional
`TimeProvider`; rate limiting and circuit breaking preserve the supplied shared
instances and their state. Existing APIs and retry defaults remain unchanged.

### Documentation

- Added immediate-execution examples and shared-resource ownership guidance.

## [0.1.2](https://github.com/Jaxelr/Laminar/releases/tag/v0.1.2) - 2026-10-31

### Added

- `ExecuteAsync` extensions on `IOperationResilience` for one-line execution of
  `Task`- and `Task<T>`-returning delegates.
- Overloads for cancellation-aware delegates and an optional caller cancellation
  token.

The helpers return `ValueTask` or `ValueTask<T>` and reuse the existing delegate
adapters and resilience engine. Existing APIs remain unchanged.

### Documentation

- Added the complete release changelog and linked NuGet release notes to it.

## [0.1.1](https://github.com/Jaxelr/Laminar/releases/tag/v0.1.1) - 2026-10-31

### Added

- Exponential retry backoff through `RetryOptions.BackoffFactor`.
- Timer-limit capping for growing retry delays.

`BackoffFactor` defaults to 1, preserving fixed-delay behavior. Retry sequences
remain independent per invocation and delays honor caller cancellation.

### Publishing

- Stable releases now publish to both nuget.org and GitHub Packages.

## [0.1.0](https://github.com/Jaxelr/Laminar/releases/tag/v0.1.0) - 2026-10-18

### Released

- First stable release for .NET 10, retaining the operation-first resilience
  patterns introduced in the preview.
- Stable package publishing to nuget.org.

## [0.1.0-preview.1](https://github.com/Jaxelr/Laminar/releases/tag/v0.1.0-preview.1) - 2026-10-04

### Added

- Initial preview of composable, operation-first resilience for .NET 10.
- Rate limiting, retry with optional fixed delays, cooperative timeout, and
  circuit breaking.
- Custom resilience patterns through `IOperationResilience` and `ResilientWith`.
- Adapters for synchronous, `Task`-, and `ValueTask`-returning delegates, including
  cancellation-aware asynchronous operations.
