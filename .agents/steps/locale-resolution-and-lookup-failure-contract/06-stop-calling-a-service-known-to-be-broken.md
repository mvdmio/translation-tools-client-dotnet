# 06 — Stop calling a service known to be broken

Status: done

## What to build

A job doing dozens of lookups against a dead service should finish in seconds, not wait out a timeout
per lookup. After a failure that says the service itself is unreachable or broken, the client stops
calling out for one minute and answers every single-key lookup from its local fallback.

Only three things open the window: a connection failure, a timeout, and a `5xx`. A response the
service actually produced about one particular request says nothing about the service, so a `401` and
a `404` are answered and logged without suppressing the next lookup. A rejected API key returns fast
anyway, so it needs no suppression.

The window is process-wide rather than per locale or per key, because what opens it is a statement
about the service and not about one request. It is tracked against the injected `TimeProvider`, the
same clock the heartbeat already uses.

Suppression changes where an answer comes from, not what a caller can observe: a suppressed lookup
still consults the cache first, and still logs. A whole-locale lookup inside the window throws
immediately without calling out, matching the rule that it throws rather than degrades.

Nothing probes the service in the background. The first single-key lookup after the window expires is
a live call, and it either succeeds or opens a new window. The options expose neither the length of
the window nor a way to disable it.

## Acceptance criteria

- [ ] A `500` opens the window: the next lookup for a different key makes no request and returns a
      local fallback.
- [ ] A transport/connection failure and a timeout open the window the same way.
- [ ] A `401` does not open the window: the next lookup does make a request.
- [ ] A `404` does not open the window.
- [ ] Advancing the fake clock past one minute makes the next single-key lookup call out again.
- [ ] A suppressed lookup still consults the cache first and still logs.
- [ ] A whole-locale lookup inside the window throws without making a request.
- [ ] The window is process-wide, and the options expose neither its length nor a way to disable it.
- [ ] Nothing probes the service in the background.
- [ ] The full test suite is green.

## Outcome

`TranslationToolsClient` now tracks a process-wide (per client instance, shared across every
locale and key) suppression window: a `long _suppressedUntilUtcTicks` field read/written with
`Interlocked`, compared against `_timeProvider.GetUtcNow().UtcTicks` in `IsLookupSuppressed()`, and
set to now-plus-one-minute by `SuppressLookupsForOneMinute()` (`LookupSuppressionWindow =
TimeSpan.FromMinutes(1)`, not exposed on `TranslationToolsClientOptions`).

`FetchTranslationOrFallbackAsync` (the single-key path) checks `IsLookupSuppressed()` first, before
building or sending a request. When suppressed, it routes straight through the existing
`HandleDegradedLookup` with reason "the service is suppressed after a recent failure" — the same
helper every other failure class uses, so a suppressed lookup gets the same local-fallback chain,
the same logging, and the same `ThrowOnLookupError` behavior as any other degraded lookup. Because
the cache is still consulted in `GetInternalAsync` before this method is ever called, a suppressed
lookup for an already-cached key never reaches the suppression check at all — it answers from cache
exactly as it would outside the window, which is what "still consults the cache first" means in
practice.

Only three call sites open the window, matching the spec's list: the `catch (Exception exception)`
block around `_client.SendAsync` (covers both a transport/connection failure and the lookup's own
`TimeProvider`-driven timeout, since both surface as a non-caller-cancellation exception there), and
the unsuccessful-status-code branch when `(int)response.StatusCode >= 500`. A `401` and a `404`
fall through the same branch without calling `SuppressLookupsForOneMinute()`. Deserialization
failures (bad JSON, a `null` body) also do not open the window — the spec lists only connection
failure, timeout, and `5xx`, and an undeserialisable body from a `2xx` response is none of those.

`FetchLocaleAsync` (the whole-locale path, shared by `GetLocaleAsync` and `RefreshLocaleAsync`)
checks `IsLookupSuppressed()` at its top and throws `TranslationLookupException` immediately,
before building a request, when the window is open — matching "throws without calling out". A
whole-locale failure does not itself open the window (only single-key failures are classified into
the three window-opening categories); this wasn't asked for by the spec or acceptance criteria; a
whole-locale lookup already always throws regardless of the window, so nothing about the window
changes what it returns, only whether it makes a request first. `TranslationLookupException`'s XML
doc comment was broadened from "thrown by a single-key lookup with `ThrowOnLookupError`" to also
cover this whole-locale-during-suppression case, rather than introducing a second exception type.

Nothing probes the service in the background: there is no timer, no background task, and no code
path that calls `SuppressLookupsForOneMinute()` or clears the window other than the classification
points above and the passage of time past the recorded instant. The first single-key lookup after
`IsLookupSuppressed()` starts returning `false` again is a normal live call through the same code
path as any other lookup.

Tests added in
`test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/LookupSuppressionTests.cs` (new
file, using a `FakeTimeProvider` and a small `RecordingHandler` that counts requests and computes
its response from a caller-supplied `Func<int, HttpResponseMessage>`, following
`LookupTimeoutTests`'/`LookupFailureFallbackTests`' patterns):
- A `500`, a transport exception, and a timeout each open the window: the next lookup for a
  *different* key makes no request and returns its local fallback.
- A `401` and a `404` do not open the window: the next lookup does make a request.
- Advancing the fake clock past one minute makes the next lookup call out again (request count
  increments).
- Advancing the clock partway through the window with no further lookups leaves the request count
  unchanged (nothing probes in the background).
- A suppressed lookup for an already-cached key is answered from the cache without a request, and a
  suppressed lookup for a new key still logs (one entry for the failure that opened the window, a
  second for the suppressed lookup).
- A whole-locale lookup inside the window throws `TranslationLookupException` without making a
  request.

Full solution build and `dotnet test` are green: 139 unit tests (129 + 10 new), 7 integration
tests, 19 tool unit tests — no failures, no regressions.

No deviations from the step or spec.
