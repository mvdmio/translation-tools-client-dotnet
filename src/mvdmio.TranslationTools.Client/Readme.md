# mvdmio.TranslationTools.Client

Client package for the mvdmio TranslationTools HTTP translation API.

## Install

```bash
dotnet add package mvdmio.TranslationTools.Client
```

## Configure

```csharp
using mvdmio.TranslationTools.Client;

builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.EnableLiveUpdates = true;
});

var app = builder.Build();
await app.InitializeTranslationToolsClientAsync();
```

## Authentication model

- Client sends project API key through `Authorization` header.
- Server resolves project from header value.
- Project id is not part of request route.

## API routes used by client

- `GET /api/v1/translations/{locale}`
- `GET /api/v1/translations/{locale}/{key}`

Single-item requests may include `defaultValue` as query parameter.

## Translation key rules

- Regex: `^[A-Za-z0-9._-]+$`
- Allowed: letters, numbers, `.`, `_`, `-`
- Invalid keys are rejected client-side before request.

## Locale behavior

- API normalizes locale values to lowercase.
- Current client uses `CultureInfo.CurrentUICulture` for UI-oriented default overloads.
- Explicit locale overloads still send `CultureInfo.Name` as provided by the caller.
- Server-side normalization still makes `en`, `EN`, and `en-US` style inputs deterministic for lookup rules used by the API.

## Usage

```csharp
using System.Globalization;
using mvdmio.TranslationTools.Client;

var client = app.Services.GetRequiredService<TranslationToolsClient>();

var item = await client.GetAsync("home.title", new CultureInfo("en"));
var locale = await client.GetLocaleAsync(new CultureInfo("en"));
var cached = client.TryGetCached("home.title");

var syncText = Localizations.Button_Save;
var text = await Localizations.GetAsync(Localizations.Keys.Button_Save);
var fallback = await Localizations.GetAsync("checkout.title", "Checkout");
var dutch = await Localizations.GetAsync("home.title", new CultureInfo("nl-NL"));
```

## Generated localizations

Declare partial properties in a partial class. The package source generator emits implementations plus a nested `Keys` class.

```csharp
using mvdmio.TranslationTools.Client;

[Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
public static partial class Localizations
{
   [Translation(DefaultValue = "Hello")]
   public static partial string Button_Hello { get; }

   public static partial string Button_Save { get; }

   [Translation(Key = "button.save_and_close", DefaultValue = "Save and close")]
   public static partial string Button_SaveAndClose { get; }
}
```

Usage after generation:

```csharp
var label = Localizations.Button_Save;
var key = Localizations.Keys.Button_Save;
var asyncLabel = await Localizations.GetAsync(Localizations.Keys.Button_Save);
```

- Sync generated properties call `TranslationManifestRuntime` through generated code.
- Generated manifest classes also expose `GetAsync(...)` helpers.
- Sync reads are cache-only; they never trigger network fetches.
- Sync reads check the registered runtime client cache before the embedded snapshot.
- Async reads use embedded snapshot first, then network on miss.
- Sync fallback order: runtime client cache -> embedded snapshot -> manifest `DefaultValue` -> key.
- Async fallback order: embedded snapshot -> network -> manifest `DefaultValue` -> key.
- Call `InitializeTranslationToolsClientAsync()` during app startup before relying on sync generated access.

## Offline snapshot

- `translations pull` writes `.mvdmio-translations.snapshot.json` in the project root.
- The client package auto-embeds that root snapshot through a `buildTransitive` props file.
- Snapshot lookup is assembly-scoped, so generated manifests read their own assembly's embedded snapshot.
- If no embedded snapshot contains the key, sync reads fall back to manifest `DefaultValue`, then key.

## Live update cache support

- Public cache refresh/invalidation APIs:
  - `RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)`
  - `InvalidateLocale(CultureInfo locale)`
  - `Invalidate(string key, CultureInfo locale)`
- Public externally-driven cache update APIs:
  - `ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)`
  - `ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default)`
- `RefreshLocaleAsync(...)` replaces the cached locale payload and removes stale per-key entries for that locale.
- `InvalidateLocale(...)` removes the cached locale payload and all cached items for that locale.
- `Invalidate(...)` removes one cached item and removes it from the cached locale dictionary if present.
- `ApplyLocaleUpdateAsync(...)` replaces the cached locale payload without fetching from HTTP.
- `ApplyUpdateAsync(...)` updates a single cached item and updates the cached locale dictionary if that locale payload is already cached.

## Push transport status

- Built-in live transport is available when `EnableLiveUpdates = true`.
- Transport flow:
  - fetch socket token from `GET /api/v1/translations/socket-token`
  - connect to `/ws/translations?token=...`
  - ignore `{ "type": "connected" }`
  - apply `{ "type": "translation-updated", "locale": "en", "key": "home.title", "value": "Hello" }`
- Current transport applies single-item cache updates only.
- Current implementation reconnects best-effort and fetches a fresh socket token before reconnecting.
- Still missing for richer live sync:
  - full-locale snapshot messages
  - explicit invalidation messages
  - ordering/version metadata
  - replay/resync contract after disconnects

## Create-on-read and `defaultValue`

- API single-item fetch creates a missing translation row.
- Missing row starts with `null` value.
- Generated `GetAsync(..., defaultValue: ...)` helpers use the internal single-item overload that appends `defaultValue` to the request.
- Server seeds the default-locale row only when its current value is missing or empty.
- Server never overwrites an existing non-empty default-locale value.

## Caching

- Client stores fetched single-item payload per locale/key cache entry.
- Client stores fetched locale payloads per locale cache entry.
- Repeated single-item requests reuse cached values for the process lifetime.
- Repeated locale requests reuse cached locale dictionaries for the process lifetime.
- Locale initialization fetches locale payloads and hydrates per-item cache entries.
- Locale fetches also hydrate per-item cache entries used by sync APIs.
- Refreshing a locale replaces both the locale payload cache and its per-item entries.
- External pushed updates can be applied into the same cache through `ApplyLocaleUpdateAsync(...)` or `ApplyUpdateAsync(...)`.
- Sync APIs (`TryGetCached`, generated localization properties) read only from the local runtime cache and embedded snapshot; they never fetch over HTTP.
- Only the built-in in-memory dictionary cache remains.

## Initialize behavior

```csharp
await app.Services.InitializeTranslationToolsClientAsync();
```

- Optional but recommended.
- Preloads configured supported locales.
- If `SupportedLocales` is not configured explicitly, registration tries to copy supported cultures from ASP.NET `RequestLocalizationOptions`.

## Current public API

- `ITranslationToolsClient.Initialize(CancellationToken)`
- `ITranslationToolsClient.TryGetCached(string)`
- `ITranslationToolsClient.TryGetCached(string, CultureInfo)`
- `ITranslationToolsClient.GetAsync(string key, CancellationToken)`
- `ITranslationToolsClient.GetAsync(string key, CultureInfo locale, CancellationToken)`
- `ITranslationToolsClient.GetLocaleAsync(CultureInfo locale, CancellationToken)`
- `ITranslationToolsClient.RefreshLocaleAsync(CultureInfo locale, CancellationToken)`
- `ITranslationToolsClient.InvalidateLocale(CultureInfo locale)`
- `ITranslationToolsClient.Invalidate(string key, CultureInfo locale)`
- `ITranslationToolsClient.ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?>, CancellationToken)`
- `ITranslationToolsClient.ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken)`
- `TranslationToolsClientOptions.EnableLiveUpdates`
- generated manifest properties and generated manifest `GetAsync(...)` helpers
- `InitializeTranslationToolsClientAsync(CancellationToken)`

## OpenAPI and docs

- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar/`
- Versioned Scalar route: `/scalar/v1`

## Non-.NET clients

- Send `Accept-Encoding: gzip` for locale fetches.
- Cache responses according to local application needs.
