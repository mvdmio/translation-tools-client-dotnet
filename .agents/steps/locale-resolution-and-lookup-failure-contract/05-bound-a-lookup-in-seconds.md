# 05 — Bound a lookup in seconds

Status: done

## What to build

"Never throws" must not become "never returns". A single-key lookup gives up after five seconds by
default, and `LookupTimeout` changes that. A synchronous property access can no longer stall a thread
for the length of an HTTP stack's own timeout.

The bound is applied with a cancellation token the client creates from its injected `TimeProvider`,
linked to the caller's token — not by setting the timeout on the `HttpClient`. The `HttpClient` is
supplied by the consumer through the HTTP client factory and its timeout is not the client's to
overwrite, and a token driven by the injected clock can be driven by a fake clock in a test while an
`HttpClient` timeout cannot.

A lookup that runs out of time is a failure like any other: it logs and answers with the local
fallback, or throws when the application opted into that. The client keeps its own timeout distinct
from the caller's cancellation, so a caller who cancels still gets a cancellation.

A whole-locale lookup is not bounded by this. It runs during initialization or on an explicit call,
where a caller waiting is expected and five seconds is not a safe assumption about how long a full
locale takes to transfer.

## Acceptance criteria

- [x] A handler that never completes ends the lookup once the fake clock passes the timeout, and the
      lookup returns a local fallback.
- [x] The default bound is five seconds, and `LookupTimeout` changes it.
- [x] The client does not modify the timeout of the `HttpClient` it was handed.
- [x] A caller's cancellation still propagates as a cancellation rather than being reported as the
      client's timeout.
- [x] A timeout degrades and logs like any other failure, and throws when `ThrowOnLookupError` is set.
- [x] A whole-locale lookup is not bounded by `LookupTimeout`.
- [x] The README options table gains a `LookupTimeout` row.
- [x] The full test suite is green.

## Outcome

Added `TranslationToolsClientOptions.LookupTimeout` (`TimeSpan`, default five seconds), documented
next to `ThrowOnLookupError`.

`FetchTranslationOrFallbackAsync` now bounds the request with a `TimeProvider`-derived token: it
constructs `new CancellationTokenSource(Options.LookupTimeout, _timeProvider)` (the built-in .NET 8+
constructor overload that schedules its timer through the given `TimeProvider`, which is exactly
what lets a `FakeTimeProvider` drive it deterministically in a test) and links it with the caller's
own `cancellationToken` via `CancellationTokenSource.CreateLinkedTokenSource`. The linked token is
what's passed to `_client.SendAsync`, not `cancellationToken` directly, and `HttpClient.Timeout` is
never touched.

The existing cancellation-vs-failure split from step 03 needed no change to handle this correctly:
both `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` filters
already check the *original* caller token, not the linked one. When the timeout timer fires, the
linked token cancels and `SendAsync` throws `OperationCanceledException`, but the caller's own token
is still uncancelled, so the `when` filter fails and the exception falls through to the general
`catch (Exception exception)` block — which is the same path a transport exception takes, so a
timeout degrades and logs (and throws under `ThrowOnLookupError`) exactly like any other failure,
with no new classification branch needed. A genuine caller cancellation still hits the `when` filter
first and rethrows, regardless of whether the timeout has also elapsed.

`FetchLocaleAsync`/`GetLocaleAsync` (whole-locale) were left untouched — they still call the
original `FetchAsync<T>` with only the caller's token, so they are not bounded by `LookupTimeout`.

Added the `LookupTimeout` row to the options table in
`src/mvdmio.TranslationTools.Client/Readme.md`.

Tests added in
`test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/LookupTimeoutTests.cs` (new file,
with its own private `NeverCompletingHandler` mirroring the one in `LookupFailureFallbackTests`),
using a `FakeTimeProvider` passed to the internal constructor:
- A handler that never completes: advancing the fake clock by the default five seconds (started
  before the clock is advanced, since the `CancellationTokenSource(TimeSpan, TimeProvider)`
  constructor runs synchronously before the first real `await` inside `SendAsync`, so the timer is
  already registered once the returned `Task` exists) ends the lookup with the local fallback.
- `LookupTimeout` set to 200ms overrides the default; advancing by that amount (not five seconds)
  ends the lookup.
- Constructing a client with a stub `HttpMessageHandler` and asserting `HttpClient.Timeout` is
  unchanged after construction, covering "the client does not modify the timeout of the `HttpClient`
  it was handed."
- A timeout with `ThrowOnLookupError` set throws `TranslationLookupException`.
- A timeout logs one `Warning` line naming the key, same as any other degraded lookup.
- A caller's own cancellation (via a real, non-fake `CancellationTokenSource`) still propagates as
  `OperationCanceledException` rather than being reported as the client's timeout.
- `GetLocaleAsync` with `LookupTimeout` set to 1ms and the clock advanced ten seconds past it stays
  pending (asserted via `task.IsCompleted` after a short real delay), then is ended by the caller's
  own token — confirming a whole-locale lookup is not bounded by `LookupTimeout`.

Full solution build and `dotnet test` are green: 129 unit tests (122 + 7 new), 7 integration tests,
19 tool unit tests — no failures, no regressions.

No deviations from the step or spec.
