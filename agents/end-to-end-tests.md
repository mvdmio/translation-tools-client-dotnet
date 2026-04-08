# Client End-To-End Test Plan

## Goal

- Add a real client integration test that covers startup hydration, steady-state reads, and live-update cache mutation.
- Validate the path the app actually uses:
  - `AddTranslationToolsClient(...)`
  - `InitializeTranslationToolsClientAsync()`
  - locale preload from HTTP
  - sync and async reads from generated APIs
  - live websocket update reaching `TranslationToolsClient` cache

## Current State

- Current client tests are mostly unit tests around cache behavior, DI registration, source generation, and live-update payload parsing.
- Missing today:
  - one test that boots an app/service provider end-to-end
  - one test that proves `Initialize(...)` hydrates cache from API responses
  - one test that proves websocket `translation-updated` messages mutate the same cache later used by generated accessors

## Main Blockers In Current Production Code

### 1. Base URL is hardcoded

- `TranslationToolsClient` forces `HttpClient.BaseAddress = https://translations.mvdm.io`.
- `TranslationToolsLiveUpdateService` also hardcodes that same base URL for socket-token fetch and websocket connect.
- Result: tests cannot point the real client to a local fake server.

### 2. Live-update path uses real `ClientWebSocket`

- `TranslationToolsLiveUpdateService` creates `new ClientWebSocket()` directly.
- That means in-memory HTTP fakes are not enough for the live-update path.
- A true integration test needs either:
  - a real loopback websocket server, or
  - a new abstraction seam for websocket creation.

### 3. One-origin assumption matters

- Current flow fetches socket token and opens websocket against the same base URL.
- A split setup like `WireMock for HTTP + separate websocket host` becomes awkward unless production code gains separate HTTP and websocket endpoint options.

## Recommendation

- Use one real loopback ASP.NET Core host with Kestrel for the first startup+live-update integration test.
- Do not make WireMock the primary tool for this scenario.
- Reason: this test needs both HTTP and websocket on one origin, and current production code already assumes that.

WireMock still makes sense later for HTTP-only integration tests such as:

- startup hydration only
- single-item fetch behavior
- HTTP failure/retry cases that do not involve websocket transport

## Minimal Production Seams To Add First

### 1. Add internal-only test endpoint override

Add an internal option or internal endpoint resolver used only by tests:

- not publicly configurable through normal consumer config
- default stays `https://translations.mvdm.io`

Use it in:

- `TranslationToolsClient`
- `TranslationToolsLiveUpdateService`

Notes:

- Keep current behavior as the default production path.
- Test code can set the override through `InternalsVisibleTo`, internal DI seams, or an internal setter.
- Do not expose a public `BaseUrl` property that library consumers can bind from app configuration.
- Derive websocket URI from the effective internal base URI instead of from the hardcoded constant.
- `http` -> `ws`, `https` -> `wss`.

### 2. Optional: add websocket factory seam only if needed

First choice:

- avoid extra abstraction
- run a real local Kestrel host

Only add a seam like `ITranslationToolsWebSocketFactory` if loopback hosting turns out too awkward.

## Test Shape

### New test project

Add:

- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration`

Reason:

- keeps slower network-style tests separate from unit tests
- easier package references and collection-level serialization

Suggested packages:

- `Microsoft.NET.Test.Sdk`
- `xunit.v3`
- `AwesomeAssertions`
- `Microsoft.AspNetCore.App` framework reference

WireMock package is optional for this first slice.

## Test Host Design

Create a fixture that starts a real local ASP.NET Core app on a dynamic loopback port.

Fixture responsibilities:

- start Kestrel on `http://127.0.0.1:{port}`
- expose request counters / captured headers
- serve locale payloads from in-memory state
- issue a deterministic socket token
- accept websocket connection on `/ws/translations`
- allow the test to push live-update frames to the connected client
- stop cleanly after the test

Suggested endpoints:

- `GET /api/v1/translations/en`
- `GET /api/v1/translations/socket-token`
- `GET /ws/translations?token=test-token`

Suggested in-memory state:

- locale dictionary keyed by locale + `TranslationRef`
- request counters for hydration assertions
- connected websocket list or a single active websocket

## First Test Case

### Name

- `InitializeTranslationToolsClientAsync_ShouldHydrateCache_AndApplyLiveUpdatesToGeneratedAccessors`

### Arrange

Fake server state starts with locale `en`:

- `/Localizations.resx` + `Button.Save` = `Save from API`
- `/Resources/Shared/Errors.resx` + `404.title` = `Not found from API`

App under test:

- build a real `WebApplication`
- call `AddTranslationToolsClient(options => ...)`
- set:
  - `ApiKey = "test-api-key"`
  - internal test base URI override = `server.BaseUrl`
  - `SupportedLocales = [ new CultureInfo("en") ]`
  - `EnableLiveUpdates = true`

Reuse existing generated fixture types if possible:

- `Fixture.App.Localizations`
- `Fixture.App.Resources.Shared.Errors`

### Act

1. call `await app.InitializeTranslationToolsClientAsync()`
2. assert startup hydration via:
   - `client.GetLocaleAsync(new CultureInfo("en"))`
   - generated sync read `Localizations.Button_Save`
   - generated async read `Errors.GetAsync("404.title", new CultureInfo("en"))`
3. send websocket payload:

```json
{"type":"translation-updated","origin":"/Localizations.resx","locale":"en","key":"Button.Save","value":"Save live"}
```

4. wait until cache reflects update

### Assert

- hydration endpoint called exactly once during startup
- socket-token endpoint called once
- locale snapshot contains initial API values after startup
- `Localizations.Button_Save == "Save from API"` before live update
- after websocket frame:
  - `client.TryGetCached(Localizations.Keys.Button_Save, new CultureInfo("en"))` has `Save live`
  - `Localizations.Button_Save == "Save live"`
  - `GetLocaleAsync("en")` snapshot also contains `Save live`

## Waiting Strategy

- Do not use blind `Task.Delay(...)` in assertions.
- Add a tiny polling helper in the integration test project:
  - retry until predicate passes or timeout expires
  - include last observed value in failure message

Reason:

- websocket receive loop is background-driven
- test should wait for state convergence, not fixed timing

## Isolation / Reliability Notes

### 1. Serialize these tests

- `Translations.SetClient(...)` is static global state.
- Integration tests using generated sync APIs should run in a dedicated xUnit collection with parallelization disabled for that collection.

### 2. Keep assertions on one locale first

- First slice should only preload `en`.
- Add multi-locale cases later after the basic path is stable.

### 3. Verify auth header too

- Fake server should assert `Authorization: test-api-key` on HTTP routes.
- This catches regressions in DI/client startup configuration.

## Follow-Up Coverage After First Test

After the first happy-path test, add:

1. startup with `EnableLiveUpdates = false`
2. websocket update for unknown locale should be ignored
3. websocket update with missing `origin` should throw and be treated as an invalid payload
4. reconnect flow after websocket disconnect
5. startup hydration for more than one supported locale
6. generated sync read fallback when startup hydration fails

## Where Code Will Likely Change

Production:

- `src/mvdmio.TranslationTools.Client/TranslationToolsClientOptions.cs`
- `src/mvdmio.TranslationTools.Client/TranslationToolsClient.cs`
- `src/mvdmio.TranslationTools.Client/Internal/TranslationToolsLiveUpdateService.cs`

Tests:

- new integration test project under `test/TranslationTools/`
- fixture for local HTTP + websocket host
- first end-to-end startup/live-update test

## Why This Plan

- Covers the real app startup path instead of only isolated units.
- Validates cache hydration and later cache mutation against the same client instance.
- Reuses the generated localization surfaces, so the test exercises behavior users actually consume.
- Avoids introducing extra production abstractions unless the real-host approach proves insufficient.

## WireMock Decision

- Good fit: HTTP-only integration coverage.
- Weak fit: same-origin websocket live-update coverage with the current production design.
- Recommendation: start with a real local ASP.NET Core host for this scenario; add WireMock later where it clearly reduces setup cost.
