# Changelog

All releases, including prereleases, are listed below in newest-first order.

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
