# Locale resolution and lookup failure contract

Status: ready-for-agent

## Problem Statement

An application that reads a translation from a background job cannot read one at all. Every
translation lookup throws, and the exception surfaces from what looks like a plain string property.

The cause is an empty locale. A lookup defaults its locale to the thread's current UI culture. A
scheduled job commonly runs under the invariant culture, whose name is an empty string, and the
client puts that name straight into the request path. The path collapses, the service matches no
route, and returns `404`. The client treats any unsuccessful response as fatal and throws.

Two consequences make this worse than a missing string.

First, the failure is total rather than partial. It is not one key that cannot be found; it is
every key, because the locale is empty for all of them. One consuming application ran two nightly
jobs for nineteen days, and all 38 lookups per night failed. The translations existed on the
service the whole time, in every locale the application uses.

Second, the failure is fatal rather than cosmetic. A translation is display text. A caller reading
one has no useful way to recover from a failure, and usually is not written to expect one, because
the call site is a property access. So a translation service that is briefly unreachable takes down
the work the caller was actually doing.

The client already holds everything it needs to answer these lookups without the service. The
source generator embeds the application's own `.resx` text in the generated class, and hands it to
the client on every call. Today the client forwards that text to the service and never reads it.
The README already lists "fallback to local `.resx` resources" as a feature of the package. At
runtime, that promise is unmet.

## Solution

The client gets one stated contract for locale resolution, and one stated contract for failure.

**Locale resolution.** A locale the client cannot use is replaced before the request is built. In
practice that means one case: a locale whose name is blank, which is the invariant culture. It is
replaced by the configured `DefaultLocale`. The result is the **effective locale** — the locale the
lookup actually runs against. Nothing else about a caller's locale changes. A locale the caller
names is used as named.

**Failure.** A translation lookup never throws. Any failure — a missing key, an unreachable
service, a server error, a timeout, or a rejected API key — is logged and answered with the
**local fallback**: the `.resx` text the application shipped with. Applications that want the
opposite set `ThrowOnLookupError`.

Two safeguards stop "never throws" from becoming "never returns", which for a job doing dozens of
lookups against a dead service would be worse than the original bug. A lookup gives up after five
seconds. And a failure that says the service itself is unreachable or broken suppresses further
calls for one minute, so the second through thirty-eighth lookups in a run answer from local text
immediately instead of each waiting out a timeout.

For the job that prompted this work, the effect is that the job runs, sends its email in the right
language, and leaves a warning in the log. Nobody has to change the job, and nobody has to change
the scheduling library that chose the invariant culture on purpose.

## User Stories

1. As a developer whose code runs in a scheduled background job, I want a translation lookup to
   return text rather than throw, so that the job completes.
2. As a developer whose code runs under the invariant culture, I want the client to substitute the
   configured default locale, so that the request it builds is a valid one.
3. As a developer who set `DefaultLocale` because the README said it is "the locale used when no
   specific locale can be resolved", I want the client to actually use it, so that the option means
   what it says.
4. As a developer reading a translation through a generated property, I want the local `.resx` text
   used when the service cannot answer, so that a service outage costs me nothing.
5. As a developer whose application ships Dutch `.resx` text, I want a failed Dutch lookup to
   return my Dutch text rather than the neutral English, so that a Dutch reader still reads Dutch.
6. As a developer whose application ships only neutral `.resx` text, I want a failed lookup to
   return the neutral value, so that the reader gets real words rather than a key name.
7. As a developer with neither local text nor a served value, I want the translation key returned
   as a last resort, so that the string is never null or empty.
8. As a developer, I want the translation service being briefly unreachable to leave no trace in my
   application's behaviour beyond a log line, so that its availability is not my availability.
9. As a developer, I want a lookup to give up in seconds rather than in minutes, so that a
   synchronous property access cannot stall a thread for a minute and a half.
10. As a developer whose job performs dozens of lookups, I want the client to stop calling a service
    it has just found to be broken, so that the job finishes in seconds rather than in hours.
11. As a developer, I want the client to try the service again a minute later, so that a recovered
    service is picked up without restarting my application.
12. As a developer, I want a translation that failed to load to be retried on the next call rather
    than remembered as a failure, so that the real text appears as soon as the service answers.
13. As an operator, I want every degraded lookup to name the key and the effective locale in the
    log, so that I can tell which translation degraded and for whom.
14. As an operator, I want a rejected API key logged more loudly than a missing translation, so that
    a misconfigured deployment is distinguishable from an ordinary gap in the translations.
15. As an operator, I want a failing lookup to say the service could not answer rather than to say
    the translation is missing, so that I do not go looking for a key that exists.
16. As a developer who wants failures to be loud, I want an option that makes a lookup throw, so
    that I can choose the old behaviour deliberately.
17. As a developer, I want that option named and shaped like the placeholder option I already know,
    so that the package has one way of expressing "throw instead of degrade".
18. As a developer who misconfigures `DefaultLocale` to a blank or unusable value, I want the client
    to fail when it is constructed, so that a broken fallback is not discovered one lookup at a
    time in production.
19. As a developer calling the client interface directly with no local text, I want a response whose
    value is null rather than an exception, so that the contract does not change with the call site.
20. As a developer asking for a whole locale at once, I want an error when the service cannot answer,
    so that I am not handed an empty set of translations that looks like a real answer.
21. As a developer whose application starts while the service is down, I want startup to continue,
    so that a translation outage is not a failed deployment.
22. As a developer, I want local fallback text to have its `{token}` placeholders filled in, so that
    degraded text is still readable rather than showing raw tokens.
23. As a developer using the fluent builder for a key looked up by string, I want the same failure
    contract, so that behaviour does not depend on which entry point I chose.
24. As a developer, I want the client to keep sending my local `.resx` values to the service on a
    successful lookup, so that reading local text as a fallback does not stop the service from being
    seeded.
25. As a developer reading the README, I want the local fallback section to describe what the client
    does at runtime, so that I can rely on the documented behaviour.
26. As a developer upgrading the package, I want no source change required, so that the fix arrives
    as a version bump.
27. As a maintainer, I want the reason a lookup never throws recorded as a decision, so that nobody
    later "fixes" the client by making it throw again.
28. As a maintainer, I want the words for these ideas fixed in the glossary, so that the next
    conversation about locales uses one vocabulary.

## Implementation Decisions

### Locale resolution

- `TranslationToolsClient` resolves the effective locale once, at the point it currently reads
  `locale.Name`, before any cache lookup or request is built. Every path that reads a locale name
  goes through it: the single-key lookup, the whole-locale lookup, the refresh performed during
  initialization, and the cache-inspection and invalidation helpers.
- Resolution replaces a locale whose name is blank with `Options.DefaultLocale`. It does nothing
  else. It does not walk the culture parent chain: a caller asking for `nl-BE` gets `nl-BE`, and if
  the service does not hold that locale, the lookup misses. Which locales exist is the service's
  business.
- Because resolution happens before the cache is consulted, the cache is keyed by the effective
  locale. The empty string stops being a cache bucket. A lookup made under the invariant culture and
  a lookup made under the default locale share one entry.
- `Options.DefaultLocale` is validated when the client is constructed, next to the existing `ApiKey`
  check, and throws if it is blank or is not a locale the runtime recognises. A fallback that is
  itself broken must fail at startup.
- The existing special case in `GetSupportedLocales`, which drops the invariant culture from the
  set of locales to preload, stays. Resolution and preloading answer different questions:
  resolution decides what one lookup runs against, and preloading decides which locales are worth
  fetching up front. Preloading the default locale twice would be waste.

### The fallback chain

- A single-key lookup already receives a neutral value and a dictionary of the application's own
  `.resx` text for that key, keyed by locale. The client starts reading that dictionary instead of
  only forwarding it.
- The chain, in order: the value the service returned; then the dictionary entry for the effective
  locale; then the neutral value; then the translation key.
- The dictionary is matched on the effective locale's exact name. `nl-NL` does not find an entry
  stored under `nl`. This is the same rule as locale resolution, so the client has one rule for
  matching locales rather than a local rule and a remote rule that disagree. An application whose
  users are on `nl-NL` names its file for `nl-NL`, or sets `DefaultLocale` to `nl`.
- The dictionary the generator emits already uses a case-insensitive comparer, so casing does not
  need handling here.
- The client keeps sending the neutral value and the dictionary as query parameters on a successful
  fetch. Reading them locally is added behaviour, not a replacement, so the service is still seeded.
- `Translations.Get` currently ends in `?? defaultValue ?? translation.Key`. That is a partial
  implementation of the chain at the wrong layer. The client owns the whole chain, and the line in
  `Translations` becomes a last resort that no longer fires in practice. It stays, because the
  client interface allows an implementation that returns a null value.
- Placeholder substitution is unchanged and runs over whatever the chain produced, so a `{token}` in
  local text still renders.
- On the fluent-builder path for a key looked up by string there is no dictionary, so the chain is
  the supplied neutral value, then the key.

### Failure handling

- `FetchAsync` stops calling `EnsureSuccessStatusCode` for single-key lookups. An unsuccessful
  response, a transport exception, and a timeout are all handled rather than thrown.
- A single-key lookup never throws. Failures are classified only to decide the log level and whether
  the cooldown opens, never to decide whether to throw.
- `Options.ThrowOnLookupError`, default `false`, makes a single-key lookup rethrow instead of
  returning a local fallback. Named and shaped after the existing `ThrowOnPlaceholderError`.
- A cancellation the caller requested is not a failure and is never swallowed. The client
  distinguishes its own timeout from the caller's cancellation token and lets the caller's
  cancellation propagate, as the heartbeat loop already does.
- A whole-locale lookup keeps throwing. It has no per-key neutral value to fall back to, and an
  empty set of translations is a worse answer than an error. Application startup already catches and
  logs everything, so a service that is down at boot stays non-fatal without further work.
- A response the client cannot deserialise is a failure like any other, and degrades.

### Timeout

- A single-key lookup is bounded at five seconds by default, and `Options.LookupTimeout` changes it.
- The bound is applied with a cancellation token the client creates from its injected
  `TimeProvider`, linked to the caller's token — not by setting `HttpClient.Timeout`. Two reasons:
  `HttpClient` is supplied by the consumer through `IHttpClientFactory` and its timeout is not the
  client's to overwrite, and a `TimeProvider`-driven token can be driven by a fake clock in a test
  while `HttpClient.Timeout` cannot.
- A whole-locale lookup is not bounded by this. It runs during initialization or on an explicit
  call, where a caller waiting is expected and where five seconds is not a safe assumption about how
  long a full locale takes to transfer.

### Suppression after a failure

- After a failure that says the service itself is unreachable or broken, the client stops calling
  out for one minute and answers every single-key lookup from its local fallback. `Options` exposes
  neither the window nor a way to disable it in this change.
- Only three things open the window: a connection failure, a timeout, and a `5xx`. A response the
  service actually produced about one request never opens it, so a `401` and a `404` are answered
  and logged without suppressing the next lookup. A rejected API key returns fast, so it needs no
  suppression.
- The window is process-wide, not per locale or per key, because what opens it is a statement about
  the service rather than about one request.
- A suppressed lookup still consults the cache first, and still logs, so suppression changes where
  the answer comes from and not what the caller can observe.
- A whole-locale lookup inside the window throws immediately without calling out, matching the rule
  that it throws rather than degrades.
- Nothing probes the service in the background. The first single-key lookup after the window expires
  is a live call, and it either succeeds or opens a new window.
- The window is tracked against the injected `TimeProvider`, which the client already holds for the
  heartbeat.

### Logging

- `TranslationToolsClient` takes an optional `ILogger`, supplied from the container the way
  `PlaceholderRuntime` already receives one at startup. Without a logger, nothing is logged. It also
  replaces the `Trace.WriteLine` the heartbeat currently uses.
- One log line per degraded lookup. It names the translation key, the effective locale, and why the
  lookup degraded. The message distinguishes "the service could not answer" from "the service has no
  value for this key", so a reader does not go hunting for a key that exists.
- A rejected API key logs at `Error`. Every other degraded lookup logs at `Warning`. Nothing is
  rate-limited, suppressed, or summarised: a job doing 38 lookups a night writes 38 lines, and
  filtering log volume belongs to the consumer.

### Observability the client does not add

- `TranslationItemResponse` gains no field describing where its value came from. A caller using the
  client interface directly cannot tell a degraded answer from a key the service holds with no value
  yet, other than by reading the log. This is accepted rather than overlooked; no caller has asked
  to branch on it, and the generated accessors return a bare string.
- A degraded lookup is not reported to the service as a missing key. The client already seeds the
  service by sending the neutral value and the local dictionary on a fetch, and a degraded lookup is
  one whose fetch failed. Reporting it needs a second channel that retries, which is its own feature.

### Caching

- A local fallback is never written to the cache. Cache entries have no expiry, so caching one would
  let a single outage serve stale local text until the process restarted. Every call retries, so the
  real translation appears as soon as the service answers.
- No other change to the cache. Live updates, invalidation, and the whole-locale snapshot behave as
  they do today.

### Package and documentation

- Version goes to `3.5.0`. New behaviour, no breaking change to the public API. The version lives in
  one place, as a build property shared by every project in the solution, currently `3.4.0`. Note
  that `AGENTS.md` names two project files to bump and both belong to a different package; ignore it.
- The README options table gains `ThrowOnLookupError` and `LookupTimeout`, and corrects the
  `DefaultLocale` row to describe the behaviour that now exists.
- The README's "Local fallback" section is rewritten. It currently describes a workflow for keeping
  `.resx` files in source control, and says nothing about what the client does when the service
  cannot answer.
- The README documents the failure contract, the one-minute suppression window, and the log levels.
- `docs/adr/0001-a-translation-lookup-never-throws.md` records the decision and the rejected
  alternatives. It is written as part of this work and already exists.
- `CONTEXT.md` holds the vocabulary this spec uses: effective locale, local fallback, neutral value,
  origin, translation key, environment, global placeholder, key-scoped token.

## Testing Decisions

### What makes a good test here

A test drives `TranslationToolsClient` from the outside and asserts on what a caller can observe:
the string that comes back, whether an exception was thrown, whether the cache holds an entry,
whether a request reached the service, and what was logged. It does not assert on how the client
decided any of that.

Two things make this feature unusually testable through one seam, and both already exist in the
test project.

**A stub `HttpMessageHandler`** injected into the `HttpClient` the client is constructed with. Four
test files already do this, under the names `EmptySuccessHandler`, `CapturingHandler`, and
`RecordingHandler`. A handler can return any status code, throw a transport exception, delay, or
count requests. That covers every failure class in this spec, and it also proves the request path
the client built — which is the original bug, since the failure was a malformed path rather than a
malformed response.

**A `FakeTimeProvider`** passed to the client's internal constructor, which already accepts a
`TimeProvider` for the heartbeat. `HeartbeatAndEnvironmentTests` establishes the pattern of
advancing a fake clock and asserting on request counts. The timeout and the one-minute window are
both driven from this clock, which is the reason the timeout is implemented with a
`TimeProvider`-derived cancellation token rather than `HttpClient.Timeout`.

No new production seam is needed. The one new test-side helper is a fake `ILogger` that records
level and message, so log assertions stay behavioural.

### What is tested

Locale resolution:

- A lookup under the invariant culture requests the path for `DefaultLocale`, asserted on the URL the
  stub handler received. This is the regression test for the reported bug.
- A lookup under the invariant culture and a lookup under the default locale share a cache entry, so
  the second makes no request.
- A named locale is requested as named, and no parent-chain request follows a miss.
- Constructing a client with a blank or unrecognised `DefaultLocale` throws, alongside the existing
  `ApiKey` test.

The fallback chain:

- A `404` returns the local dictionary entry for the effective locale.
- A `404` with no dictionary entry for that locale returns the neutral value.
- A `404` with neither returns the translation key.
- A dictionary holding `nl` does not satisfy a lookup for `nl-NL`, which falls through to the
  neutral value.
- A successful lookup still sends the neutral value and the dictionary as query parameters.
- Placeholder tokens in local fallback text are substituted.

Failure handling:

- Each failure class — `401`, `404`, `500`, a transport exception, a timeout, an undeserialisable
  body — returns a local fallback and does not throw.
- `ThrowOnLookupError` makes each of those throw.
- A caller's cancellation propagates as a cancellation rather than degrading.
- A whole-locale lookup throws on failure rather than returning an empty snapshot.

Timeout and suppression:

- A handler that never completes ends the lookup once the fake clock passes the timeout, and the
  lookup returns a local fallback.
- A `500` opens the window: the next lookup for a different key makes no request and returns a local
  fallback.
- A `401` does not open the window: the next lookup does make a request.
- A `404` does not open the window.
- Advancing the fake clock past one minute makes the next lookup call out again.
- A whole-locale lookup inside the window throws without making a request.

Caching and logging:

- A local fallback leaves the cache empty for that key, and a subsequent successful lookup returns
  the served value.
- A degraded lookup logs one line naming the key and the effective locale.
- A `401` logs at `Error`; a `404` logs at `Warning`.
- With no logger supplied, a degraded lookup still returns its fallback.

Integration:

- The existing integration host, which serves locales from a real in-process web application, gains
  a case where a job-like lookup runs under the invariant culture end to end through a generated
  accessor and returns the served value. This is the closest a test gets to the reported failure.

Prior art to follow: `TranslationToolsClientBehaviorTests` for driving the client with a stub
handler, `HeartbeatAndEnvironmentTests` for the fake clock and request counting,
`TranslationToolsClientCacheTests` for cache assertions, and `StartupAndLiveUpdateIntegrationTests`
with `TranslationToolsIntegrationTestHost` for the in-process service.

## Out of Scope

- **Culture parent-chain fallback.** Resolving `nl-BE` to `nl`, remotely or locally, is a separate
  feature with its own caching consequences. Decided against in this change.
- **Removing the network call from synchronous property access.** A generated property performs a
  synchronous wait over an asynchronous HTTP call, and a cache-only accessor with background
  refresh is probably the better shape. It is a larger change, it needs its own design session, and
  it does not fix this bug on its own: under the invariant culture the cache bucket is empty, so a
  cache-only accessor would serve local text forever until locale resolution exists anyway.
- **Failing application startup on a rejected API key.** Startup currently catches and logs
  everything, and that stays. Making a bad key fail a deployment is a larger promise than this
  change should make.
- **Reporting degraded lookups to the service** as missing keys, alongside the existing key and
  global orphan tracking. Needs a separate channel with its own retry behaviour.
- **Provenance on the response type.** No field saying whether a value came from the service, the
  cache, or local text.
- **Build-time verification that a key exists on the service.** Rejected: it makes every build
  depend on a live service, and the reported failure was not a missing key.
- **Changes to the scheduling library** that chose the invariant culture for cron jobs. That choice
  is deliberate and documented, and this change is what makes it safe.
- **Log rate limiting, suppression, or summarising.**
- **A configurable or disableable suppression window.**
- **Bounding the whole-locale lookup with a timeout.**
- **Cache expiry.** Entries still live until a live update, an invalidation, or a restart.
- **Fixing `AGENTS.md`,** which describes a different package entirely.

## Further Notes

The bug is not that the client mishandled a missing translation. The translations existed, in every
locale the application uses, for all nineteen days. The `404` meant "no such endpoint", because the
empty locale collapsed the request path — a fact worth keeping in mind while reading the stack
trace, which otherwise reads as a missing key.

Two of the decisions here are really the completion of decisions someone already made. The
`DefaultLocale` option exists, defaults to `en`, and is documented in the README as "the locale used
when no specific locale can be resolved" — and no client code reads it. The README lists fallback to
local `.resx` resources as a feature of the package, and the generator already delivers that text to
every call site. In both cases the contract was written down and never wired up. This spec wires it.

**One open question, with a default.** Preloading and resolution are separate, and this spec keeps
them separate: resolution decides what one lookup runs against, and preloading decides which locales
are fetched at startup. The consequence is worth naming. An application that configures no supported
locales falls back to the thread's current UI culture, and that culture is then dropped if it is the
invariant one. So a console or worker application running entirely under the invariant culture
preloads nothing, and every lookup is a live call that a cache miss cannot avoid. The default this
spec assumes is to leave that alone, because the fix makes those live calls safe. The alternative is
for preloading to use the effective locale too, which would warm the cache for exactly the
applications this bug affected, at the cost of one request at startup for applications that never
read a translation. Worth a decision before implementing, but it does not block the rest.

There is a symmetry worth noticing between the parts of the client that already survive failure and
the part that does not. A failed heartbeat is caught, traced, and retried on the next tick. A failed
startup is caught and logged. An unresolved placeholder degrades to its raw token and logs a
warning, unless the application opts into throwing. A failed lookup was the one path that killed the
caller, and it is the path where degrading is most obviously correct, because the correct text is
already in memory.
