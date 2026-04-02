# TranslationTools .NET Packages

Public .NET packages for working with the TranslationTools API and `.resx`-backed generated localization APIs.

Current client package version: `1.0.3`.

## Packages

- `mvdmio.TranslationTools.Client` - API client, DI helpers, `.resx` runtime fallback, and source-generated resource APIs
- `mvdmio.TranslationTools.Tool` - .NET tool for initializing config and syncing project `.resx` files with TranslationTools
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
- `src/mvdmio.TranslationTools.Client.SourceGenerator` - source generator used by the client package for `.resx`-backed resource APIs
- `src/mvdmio.TranslationTools.Tool` - command-line tool for `.resx` sync workflows
- `agents/plans` - working design plans for upcoming client and tooling changes
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

var title = Errors.Title;
var fetchedTitle = await Errors.GetAsync(Errors.Keys.Title);
var locale = await app.Services.GetRequiredService<ITranslationToolsClient>().GetLocaleAsync(new System.Globalization.CultureInfo("en"));
```

Define translations in `.resx` files:

```text
Errors.resx
Admin/Labels.resx
```

Example `Errors.resx`:

```xml
<data name="title" xml:space="preserve">
  <value>Error</value>
</data>
<data name="save.button" xml:space="preserve">
  <value>Save</value>
</data>
```

NuGet consumers receive the bundled source generator automatically.

After generation, consume strongly-typed properties and keys:

```csharp
var label = Errors.Title;
var key = Errors.Keys.Title;
var asyncLabel = await Errors.GetAsync(Errors.Keys.Title);
var adminLabel = Admin.Labels.Save_Button;
```

Offline mode details:

- Sync generated properties read from the runtime client cache first, then compiled `.resx` resources.
- Async generated helpers read runtime cache first, then compiled `.resx` resources, then network on miss.

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
```

Example `.mvdmio-translations.yml`:

```yaml
apiKey: project-api-key
defaultLocale: en
```

Pull translations into `.resx` files:

```bash
translations pull
translations pull --prune
```

`translations pull` updates project `.resx` files in place. By default it adds and updates values without deleting local entries. Use `--prune` to remove local entries and locale files that no longer exist remotely.

When existing project `.resx` files are present, `translations pull` also maps legacy normalized remote keys such as `Button_EditStreetSegments` back to their original `.resx` entry names.

Push project `.resx` state back to the API:

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
