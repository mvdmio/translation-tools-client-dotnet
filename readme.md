# TranslationTools .NET Packages

Public .NET packages for working with the TranslationTools API, origin-aware runtime client APIs, and `.resx`-driven generated localization classes.

Current client package version: `2.3.0`.

GitHub Actions release automation now uses a single workflow that builds and tests the full solution before publishing both the client and tool NuGet packages together.

## Packages

- `mvdmio.TranslationTools.Client` - API client, DI helpers, embedded snapshot bootstrap, source-generated localization support
- `mvdmio.TranslationTools.Tool` - .NET tool for initializing config, migrating `.resx` files, pulling remote `.resx` state, and pushing local `.resx` state back to TranslationTools
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
- `src/mvdmio.TranslationTools.Client.SourceGenerator` - source generator used by the client package for `.resx`-backed properties
- `src/mvdmio.TranslationTools.Tool` - command-line tool for `.resx` sync workflows
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

var client = app.Services.GetRequiredService<ITranslationToolsClient>();
var title = Localizations.Button_Save;
var fetchedTitle = await Localizations.GetAsync("Button.Save");
var locale = await client.GetLocaleAsync(new System.Globalization.CultureInfo("en"));
var refValue = await client.GetAsync(Localizations.Keys.Button_Save, new System.Globalization.CultureInfo("en"));
```

Source generation now starts from local neutral `.resx` files.

Localized `.resx` variants keep their neutral `.resx` file as `DependentUpon`, so IDEs such as Rider continue to nest files like `Localizations.nl.resx` under `Localizations.resx`.

Path handling is separator-agnostic, and Windows-style absolute project paths stay project-relative even when generation runs on Unix-based CI agents.

Generated `GeneratedCodeAttribute` metadata now uses the source generator assembly version automatically, and shared package/version metadata is centralized so the emitted version stays aligned.

Generated `.g.cs` files are now emitted to the consuming project's `obj/Generated` directory by default so package consumers can inspect and step through generated localization code while debugging.

Published packages now include Source Link and symbol package metadata so debuggers can step into the client library source as well.

`TranslationLocaleSnapshot` now derives its legacy string-key lookup surface directly from ordered snapshot items instead of maintaining a separate legacy index.

Example `Localizations.resx`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Button.Save">
    <value>Save</value>
  </data>
  <data name="Button.Cancel">
    <value>Cancel</value>
  </data>
</root>
```

After generation, consume strongly-typed properties and keys:

```csharp
var label = Localizations.Button_Save;
var key = Localizations.Keys.Button_Save;
var asyncLabel = await Localizations.GetAsync("Button.Save");
```

Offline mode details:

- Consumers should inject and use `ITranslationToolsClient` for runtime translation access.
- Generated `.resx` types use the static `Translations` facade, which receives the `ITranslationToolsClient` instance during `InitializeTranslationToolsClientAsync()`.

Live update cache support:

- Built-in live transport is available behind `EnableLiveUpdates = true`.
- Current WebSocket message contract is origin-aware:
  - `{ "type": "connected" }`
  - `{ "type": "translation-updated", "origin": "/Localizations.resx", "locale": "en", "key": "home.title", "value": "Hello" }`
- Current live transport applies single-item cache updates only.
- Current runtime still accepts missing `origin` as legacy `/Localizations.resx` compatibility.

## CLI quick start

Initialize configuration:

```bash
translations init
translations migrate
translations pull
translations push
```

Example `.mvdmio-translations.yml`:

```yaml
apiKey: project-api-key
defaultLocale: en
```

Migrate local `.resx` files into TranslationTools and refresh local files from API state:

```bash
translations migrate
```

Pull translations into local `.resx` files and refresh the embedded snapshot:

```bash
translations pull
translations pull --prune
```

Push local `.resx` values back to the API:

```bash
translations push
translations push --prune
```

Current notes:

- pull/write model is origin-aware and `.resx`-first
- push scans `.resx` files directly
- current `pull --prune` surface exists, but full remote-aligned deletion is not fully implemented yet
- current push path still contains a legacy manifest fallback for tests/compatibility when no `.resx` files exist

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

Current unit coverage includes source generation, fixture-project end-to-end generated API coverage, tool config resolution, pull/push/migrate helper logic, `.resx` parsing/writing, and origin-aware client lookup types.
