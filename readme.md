# TranslationTools .NET Packages

Public .NET packages for working with the TranslationTools API and generated localization manifests.

## Packages

- `mvdmio.TranslationTools.Client` - API client, caching, DI helpers, static `Translate` facade, and source-generated manifest support
- `mvdmio.TranslationTools.Tool` - .NET tool for initializing config, pulling manifests, and pushing manifest keys back to TranslationTools
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
});

var app = builder.Build();
await app.InitializeTranslationToolsClient();
```

Read translations at runtime:

```csharp
using mvdmio.TranslationTools.Client;

var title = await Translate.GetAsync("home.title", "Home");
var cachedTitle = Translate.Get("home.title", "Home");
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

After generation, consume strongly-typed properties and keys:

```csharp
var label = Localizations.Action_Save;
var key = Localizations.Keys.Action_Save;
```

## CLI quick start

Initialize configuration:

```bash
translations init
```

Example `.mvdmio-translations.yml`:

```yaml
apiKey: project-api-key
output: Localizations.cs
namespace: MyApp.Localization
className: Localizations
keyNaming: UnderscoreToDot
```

Pull translations into a manifest file:

```bash
translations pull
translations pull --overwrite
```

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
