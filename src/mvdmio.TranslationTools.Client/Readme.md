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

- `GET /api/v1/translations/project`
- `GET /api/v1/translations/{locale}`
- `GET /api/v1/translations/{origin}/{locale}/{key}`

- Current client also keeps legacy compatibility with `GET /api/v1/translations/{locale}/{key}` through `/Localizations.resx` defaults.

## Identity model

- Public identity type: `TranslationRef`.
- `TranslationRef` contains `Origin` and `Key`.
- `Origin` comparison is case-insensitive.
- `Key` comparison is case-sensitive.

## Locale behavior

- API normalizes locale values to lowercase.
- Current client uses `CultureInfo.CurrentUICulture` for UI-oriented default overloads.
- Explicit locale overloads still send `CultureInfo.Name` as provided by the caller.

## Usage

```csharp
using System.Globalization;
using mvdmio.TranslationTools.Client;

var client = app.Services.GetRequiredService<TranslationToolsClient>();

var item = await client.GetAsync(Localizations.Keys.Button_Save, new CultureInfo("en"));
var locale = await client.GetLocaleAsync(new CultureInfo("en"));
var cached = client.TryGetCached(Localizations.Keys.Button_Save, new CultureInfo("en"));

var syncText = Localizations.Button_Save;
var text = await Localizations.GetAsync("Button.Save");
var dutch = await Localizations.GetAsync("Button.Save", new CultureInfo("nl-NL"));
```

## Generated localizations

Source generation now starts from local neutral `.resx` files. Localized `.resx` variants are ignored for generation.

Localized `.resx` variants keep their neutral `.resx` file as `DependentUpon`, so IDEs such as Rider continue to show them nested under the neutral resource file.

Generated `.g.cs` files are emitted to the consuming project's `obj/Generated` directory by default so you can inspect and step through generated localization code while debugging.

Published packages include Source Link and symbols so debuggers can step into `mvdmio.TranslationTools.Client` source from the NuGet package.

Example `Localizations.resx`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Button.Hello">
    <value>Hello</value>
  </data>
  <data name="Button.Save">
    <value>Save</value>
  </data>
</root>
```

Usage after generation:

```csharp
var label = Localizations.Button_Save;
var key = Localizations.Keys.Button_Save;
var asyncLabel = await Localizations.GetAsync("Button.Save");
```

- Generated `Keys` are `TranslationRef` values.
- Sync generated properties call `TranslationManifestRuntime` through generated code.
- Generated resource classes also expose `GetAsync(...)` helpers.
- Sync reads are cache-only; they never trigger network fetches.
- Sync reads check the registered runtime client cache before the embedded snapshot.
- Async reads use embedded snapshot first, then network on miss.
- Sync fallback order: runtime client cache -> embedded snapshot -> generated default value -> key.
- Async fallback order: embedded snapshot -> network -> generated default value -> key.
- Call `InitializeTranslationToolsClientAsync()` during app startup before relying on sync generated access.

## Offline fallback

- `translations pull` writes `.mvdmio-translations.snapshot.json` in the project root.
- The client package auto-embeds that root snapshot through a `buildTransitive` props file.
- Snapshot lookup is assembly-scoped, so generated manifests read their own assembly's embedded snapshot.

## Live update cache support

- Public cache refresh/invalidation APIs:
  - `RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)`
  - `InvalidateLocale(CultureInfo locale)`
  - `Invalidate(TranslationRef translation, CultureInfo locale)`
- Public externally-driven cache update APIs:
  - `ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<TranslationRef, string?> values, CancellationToken cancellationToken = default)`
  - `ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default)`
- `RefreshLocaleAsync(...)` replaces the cached locale payload and removes stale per-item entries for that locale.
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
  - apply `{ "type": "translation-updated", "origin": "/Localizations.resx", "locale": "en", "key": "home.title", "value": "Hello" }`
- Current transport applies single-item cache updates only.
- Current implementation reconnects best-effort and fetches a fresh socket token before reconnecting.
- Current runtime still accepts missing `origin` as legacy `/Localizations.resx` compatibility.

## Create-on-read and `defaultValue`

- API single-item fetch creates a missing translation row.
- Missing row starts with `null` value.
- Generated `GetAsync(..., defaultValue: ...)` helpers use the internal single-item overload that appends `defaultValue` to the request.
- Server seeds the default-locale row only when its current value is missing or empty.
- Server never overwrites an existing non-empty default-locale value.

## Caching

- Client stores fetched single-item payload per locale/origin/key cache entry.
- Client stores fetched locale payloads per locale cache entry.
- Locale initialization fetches locale payloads and hydrates per-item cache entries.
- Locale fetches also hydrate per-item cache entries used by sync APIs.
- Refreshing a locale replaces both the locale payload cache and its per-item entries.
- External pushed updates can be applied into the same cache through `ApplyLocaleUpdateAsync(...)` or `ApplyUpdateAsync(...)`.
- Sync APIs (`TryGetCached`, generated localization properties) read only from the local runtime cache and compiled `.resx` resources; they never fetch over HTTP.
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
- `ITranslationToolsClient.TryGetCached(TranslationRef, CultureInfo)`
- `ITranslationToolsClient.GetAsync(TranslationRef, CultureInfo, CancellationToken)`
- `ITranslationToolsClient.GetLocaleAsync(CultureInfo, CancellationToken)`
- `ITranslationToolsClient.RefreshLocaleAsync(CultureInfo, CancellationToken)`
- `ITranslationToolsClient.InvalidateLocale(CultureInfo)`
- `ITranslationToolsClient.Invalidate(TranslationRef, CultureInfo)`
- `ITranslationToolsClient.ApplyLocaleUpdateAsync(CultureInfo, IReadOnlyDictionary<TranslationRef, string?>, CancellationToken)`
- `ITranslationToolsClient.ApplyUpdateAsync(TranslationItemResponse item, CultureInfo, CancellationToken)`
- `TranslationToolsClientOptions.EnableLiveUpdates`
- generated resource properties and generated resource `GetAsync(...)` helpers
- `InitializeTranslationToolsClientAsync(CancellationToken)`

## Current compatibility surface

- legacy string-only overloads still exist for `/Localizations.resx`
- legacy embedded snapshot JSON shape still supported
- legacy live update payloads without `origin` still supported

## OpenAPI and docs

- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar/`
- Versioned Scalar route: `/scalar/v1`
