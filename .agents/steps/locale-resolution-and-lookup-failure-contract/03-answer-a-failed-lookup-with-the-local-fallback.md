# 03 — Answer a failed lookup with the local fallback

Status: done

## What to build

A single-key translation lookup never throws. A missing key, an unsuccessful response, a transport
exception, and a body the client cannot deserialise are all handled rather than thrown, and the
caller is answered with the **local fallback**: the `.resx` text the application shipped with, which
the source generator already hands to the client on every call and which the client has so far only
forwarded.

The chain a lookup answers from, in order:

1. the value the service returned;
2. the dictionary entry for the effective locale, matched on the exact locale name — a dictionary
   holding `nl` does not satisfy a lookup for `nl-NL`, the same rule locale resolution uses;
3. the **neutral value** the caller supplied.

When none of those exist the client answers with a null value. The translation key is the last resort
one layer up, in the static `Translations` entry point, which keeps its existing
`?? defaultValue ?? translation.Key`. So a caller reading through a generated accessor never sees
null or empty, while a caller using `ITranslationToolsClient` directly with no neutral value gets a
response whose value is null rather than an exception — the consequence the ADR records.

Failures are classified only to decide how loudly to log, never to decide whether to throw. Each
degraded lookup writes exactly one line naming the translation key, the effective locale, and why it
degraded, and the message distinguishes "the service could not answer" from "the service has no value
for this key" so a reader does not go hunting for a key that exists. A rejected API key logs at
`Error`; every other degraded lookup logs at `Warning`. Nothing is rate-limited or summarised: a job
doing 38 lookups writes 38 lines.

Around the edges of that contract:

- A local fallback is never written to the cache. Cache entries have no expiry, so a cached fallback
  would outlive the outage. Every call retries, and the served value appears as soon as the service
  answers.
- A whole-locale lookup keeps throwing. It has no per-key neutral value, and an empty set of
  translations is a worse answer than an error.
- A cancellation the caller asked for is not a failure. It propagates rather than degrading, the way
  the heartbeat loop already treats the caller's token.
- Placeholder substitution is unchanged and runs over whatever the chain produced, so a `{token}` in
  local text still renders.
- A successful lookup still sends the neutral value and the dictionary as query parameters. Reading
  them locally is added behaviour, not a replacement, so the service is still seeded.
- On the fluent-builder path for a key looked up by string there is no dictionary, so the chain is
  the supplied neutral value, then the key.

The response type gains no field describing where its value came from.

## Acceptance criteria

- [ ] A `404` returns the local dictionary entry for the effective locale.
- [ ] A `404` with no dictionary entry for that locale returns the neutral value.
- [ ] A `404` with neither returns the translation key through the static entry point, while the
      client interface itself answers with a null value.
- [ ] A dictionary holding `nl` does not satisfy a lookup for `nl-NL`, which falls through to the
      neutral value.
- [ ] Each failure class — `401`, `404`, `500`, a transport exception, an undeserialisable body —
      returns a local fallback and does not throw.
- [ ] A successful lookup still sends the neutral value and the dictionary as query parameters.
- [ ] `{token}` placeholders in local fallback text are substituted.
- [ ] The fluent builder for a key looked up by string degrades to the supplied neutral value and
      then the key, so behaviour does not depend on the entry point.
- [ ] A whole-locale lookup throws on failure rather than returning an empty snapshot.
- [ ] A cancellation the caller requested propagates as a cancellation rather than degrading.
- [ ] A local fallback leaves the cache empty for that key, and a subsequent successful lookup
      returns the served value.
- [ ] A degraded lookup logs one line naming the translation key and the effective locale, and says
      the service could not answer rather than that the translation is missing when that is the case.
- [ ] A `401` logs at `Error`; a `404` logs at `Warning`.
- [ ] With no logger supplied, a degraded lookup still returns its local fallback.
- [ ] The full test suite is green.

## Outcome

`GetInternalAsync` no longer routes a single-key lookup through the throwing `FetchAsync<T>`. It now
calls a new `FetchTranslationOrFallbackAsync`, which builds the request with the (unchanged)
`BuildTranslationRequest` URL/query logic and classifies what comes back:

- A transport exception from `_client.SendAsync` (any `Exception` other than the caller's own
  cancellation) logs at `Warning` with the "the service could not answer" reason and degrades.
- An unsuccessful status code degrades and logs: `401` at `Error`, everything else (`404`, `5xx`,
  etc.) at `Warning`. `404` alone gets the "the service has no value for this key" wording; every
  other status reuses "the service could not answer".
- A body that throws on deserialization, or that deserializes to `null`, degrades at `Warning` with
  "the service could not answer".
- `OperationCanceledException` is rethrown, not degraded, whenever `cancellationToken.IsCancellationRequested`
  is true — the caller's own cancellation, distinguished from any other cancellation-shaped failure
  (e.g. an `HttpClient`-level timeout the consumer configured, which has no caller-cancellation
  signal and is therefore treated as a transport failure and degraded).

Degradation produces a fallback via the new `BuildLocalFallback`, which matches the chain from the
spec: the dictionary entry for the effective locale (matched on its exact name, skipping a
present-but-empty entry), then the supplied neutral value. It never touches the cache — the
degraded response is returned directly from `GetInternalAsync` without going through
`StoreTranslationAsync`, so the next call for the same key retries the service. A successful fetch
is unaffected and still flows through the existing cache-store path.

`GetLocaleAsync`/`FetchLocaleAsync` (whole-locale) were left untouched — they still call the
original throwing `FetchAsync<T>` with `EnsureSuccessStatusCode`, so a failed whole-locale lookup
still throws rather than degrading, per spec.

Logging is a single `_logger?.Log(level, exception, ...)` call per degraded lookup
(`LogDegradedLookup`), naming the translation key and the effective locale in the message. With no
logger supplied it's a no-op, and the lookup still returns its fallback.

`TranslationItemResponse` gained no new field; `Translations.Get`/`GetAsync` keep their existing
`?? defaultValue ?? translation.Key` last resort unchanged, and the `PlaceholderBuilder` fluent path
(which already passes `localeValues: null`) degrades straight to the supplied neutral value, then
the key, with no code change needed there.

Tests added:
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/LookupFailureFallbackTests.cs` —
  drives `TranslationToolsClient` through stub `HttpMessageHandler`s (`StatusHandler`,
  `ThrowingHandler`, `BadJsonHandler`, `NeverCompletingHandler`, `FirstFailsThenSucceedsHandler`)
  covering: the three-step fallback chain (dictionary entry / neutral value / null), the `nl` vs
  `nl-NL` exact-match rule, each failure class (`401`/`404`/`500`/transport exception/undeserialisable
  body) returning a fallback without throwing, caller cancellation propagating, whole-locale lookups
  still throwing, a degraded lookup leaving the cache empty so a later successful call is served and
  cached, and log level/wording assertions (`FakeLogger`) for the `Error`/`Warning` split and the
  "could not answer" vs "no value for this key" distinction, plus a no-logger case.
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/LookupFailureFallbackPlaceholderTests.cs`
  — new file, `[Collection("PlaceholderRuntime")]`-guarded like `PlaceholderRuntimeWiringTests` and
  `DependencyInjectionExtensionsTests` since it exercises `Translations.SetClient` and
  `PlaceholderRuntime.Configure`, both process-wide statics shared with those files. Covers `{token}`
  substitution running over degraded local-fallback text, and the fluent
  `Translations.WithPlaceholders(...).Render()` path degrading to the supplied neutral value and
  then to the key.

Full solution build and `dotnet test` are green: 113 unit tests (93 + 20 new), 7 integration tests,
19 tool unit tests — no failures, no regressions.

No deviations from the step or spec.
