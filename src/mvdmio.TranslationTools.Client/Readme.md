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
});

var app = builder.Build();
app.Services.UseTranslationToolsClient();
await app.Services.InitializeTranslationToolsClientAsync();
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
var cached = client.TryGetCached("home.title");
var syncText = Translate.Get("home.title", "Home");
var text = await Translate.GetAsync("home.title");
var fallback = await Translate.GetAsync("checkout.title", "Checkout");
var dutch = await Translate.GetAsync("home.title", new CultureInfo("nl-NL"));
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
```

- Sync generated properties call `Translate.Get(...)`.
- Sync reads are cache-only; they never trigger network fetches.
- Fallback order: cached value -> manifest `DefaultValue` -> key.
- Call `InitializeTranslationToolsClientAsync()` during app startup before relying on sync generated access.

## Create-on-read and `defaultValue`

- API single-item fetch creates a missing translation row.
- Missing row starts with `null` value.
- `Translate.GetAsync(..., defaultValue: ...)` uses the internal single-item overload that appends `defaultValue` to the request.
- Server seeds the default-locale row only when its current value is missing or empty.
- Server never overwrites an existing non-empty default-locale value.

## Caching

- Client stores fetched single-item payload per locale/key cache entry.
- Repeated single-item requests reuse cached values until cache expiry.
- Locale initialization fetches locale payloads and hydrates per-item cache entries.
- Locale payload itself is not currently stored as a separate public cache object.
- Sync APIs (`TryGetCached`, `Translate.Get`, generated localization properties) read only from the local cache.

## Cache providers

- `IMemoryCache`
- `IDistributedCache`
- `HybridCache` on `net10.0+`
- If none registered, package falls back to an internal local cache.

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
- `Translate.Get(...)`
- `Translate.GetAsync(...)`
- `InitializeTranslationToolsClientAsync(CancellationToken)`

Planned locale-level fetch/cache APIs from the implementation plan are not public yet.

## OpenAPI and docs

- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar/`
- Versioned Scalar route: `/scalar/v1`

## Non-.NET clients

- Send `Accept-Encoding: gzip` for locale fetches.
- Cache responses according to local application needs.
