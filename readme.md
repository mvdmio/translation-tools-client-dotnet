# TranslationTools .NET Packages

Public .NET packages for working with the TranslationTools API and generated localization manifests.

Current client package version: `0.6.2`.

## Packages

- `mvdmio.TranslationTools.Client` - API client, DI helpers, embedded snapshot bootstrap, and source-generated manifest support
- `mvdmio.TranslationTools.Tool` - .NET tool for initializing config, migrating `.resx` files, pulling manifests, and pushing manifest keys back to TranslationTools
- `mvdmio.TranslationTools.Client.SourceGenerator` - bundled with the client package; not shipped as a separate public package

## Install

### Client package

```bash
dotnet add package mvdmio.TranslationTools.Client
```

### CLI tool

```bash
dotnet tool install --global mvdmio.TranslationTools.Tool
```

Command name: `translations`

## What this repo contains

- `src/mvdmio.TranslationTools.Client` - reusable client library for ASP.NET and other .NET applications
- `src/mvdmio.TranslationTools.Client.SourceGenerator` - source generator used by the client package for manifest-backed properties
- `src/mvdmio.TranslationTools.Tool` - command-line tool for manifest sync workflows
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit` - unit tests for the client package
- `test/TranslationTools/mvdmio.TranslationTools.Tool.Tests.Unit` - unit tests for the CLI tool

## Client quick start

Register the client during startup:

```csharp
using mvdmio.TranslationTools.Client;

builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.EnableLiveUpdates = true;
});

var app = builder.Build();
await app.InitializeTranslationToolsClientAsync();
```

Read translations at runtime:

```csharp
using mvdmio.TranslationTools.Client;

var title = Localizations.Action_Save;
var fetchedTitle = await Localizations.GetAsync(Localizations.Keys.Action_Save);
var locale = await app.Services.GetRequiredService<ITranslationToolsClient>().GetLocaleAsync(new System.Globalization.CultureInfo("en"));
```

Define manifest-backed translations with source generation:

```csharp
using mvdmio.TranslationTools.Client;

[Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
public static partial class Localizations
{
   [Translation(DefaultValue = "Save")]
   public static partial string Action_Save { get; }

   [Translation(Key = "button.cancel", DefaultValue = "Cancel")]
   public static partial string Action_Cancel { get; }
}
```

NuGet consumers should receive the bundled source generator automatically. Version `0.4.1` retargeted the bundled generator to stable Roslyn assemblies so generation also loads correctly in non-preview SDK and IDE hosts.

After generation, consume strongly-typed properties and keys:

```csharp
var label = Localizations.Action_Save;
var key = Localizations.Keys.Action_Save;
var asyncLabel = await Localizations.GetAsync(Localizations.Keys.Action_Save);
```

Offline mode details:

- `translations pull` writes `.mvdmio-translations.snapshot.json` in the project root.
- The client package auto-embeds that snapshot into the consuming assembly.
- Sync generated properties read from the runtime client cache first, then the embedded snapshot.
- Async generated helpers use embedded snapshot first, then network on miss.

Live update cache support:

- `ITranslationToolsClient` now exposes `RefreshLocaleAsync(...)`, `InvalidateLocale(...)`, `Invalidate(...)`, `ApplyLocaleUpdateAsync(...)`, and `ApplyUpdateAsync(...)`.
- These APIs let ASP.NET/.NET apps refresh or mutate the runtime cache from an external push transport.
- Built-in live transport is available behind `EnableLiveUpdates = true`.
- Current WebSocket message contract:
  - `{ "type": "connected" }`
  - `{ "type": "translation-updated", "locale": "en", "key": "home.title", "value": "Hello" }`
- Current live transport applies single-item cache updates only.
- Still missing from the server for richer transport support: locale snapshot messages, invalidation messages, ordering/versioning, reconnect/resync guarantees.

## CLI quick start

Initialize configuration:

```bash
translations init
translations migrate
```

Example `.mvdmio-translations.yml`:

```yaml
apiKey: project-api-key
output: Localizations.cs
namespace: MyApp.Localization
className: Localizations
keyNaming: UnderscoreToDot
defaultLocale: en
```

Migrate `.resx` files into TranslationTools and regenerate the manifest:

```bash
translations migrate
```

`translations migrate` requires `.mvdmio-translations.yml` to exist already. It scans all `.resx` files under the resolved project, imports full locale/value state through the TranslationTools import API, and then reuses the pull flow to regenerate the configured manifest file.

When migrate finds a single logical resource set, imported keys keep their original `.resx` names. When multiple logical resource sets are present, migrate prefixes keys with the resource-set path and file name to keep them unique.

Pull translations into a manifest file:

```bash
translations pull
translations pull --overwrite
```

`translations pull` also refreshes the root `.mvdmio-translations.snapshot.json` file used for startup bootstrap.

Push manifest keys and default values back to the API:

```bash
translations push
```

## Package docs

- Client package README: `src/mvdmio.TranslationTools.Client/Readme.md`
- Tool package README: `src/mvdmio.TranslationTools.Tool/README.md`

## Build

```bash
dotnet build mvdmio.TranslationTools.Client.slnx
```

## Test

```bash
dotnet test mvdmio.TranslationTools.Client.slnx
```
