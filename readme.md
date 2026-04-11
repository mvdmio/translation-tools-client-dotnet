# TranslationTools .NET

`.NET` packages for working with TranslationTools translations in application code and in local `.resx` files.

## Packages

### `mvdmio.TranslationTools.Client`

Use this package in your application when you want to:

- fetch translations from the TranslationTools API
- use generated strongly typed localization classes from `.resx` files
- keep local `.resx` resources as a fallback
- optionally receive live translation updates at runtime

Install:

```bash
dotnet add package mvdmio.TranslationTools.Client
```

Basic setup:

```csharp
using mvdmio.TranslationTools.Client;

builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.DefaultLocale = "en";
   options.EnableLiveUpdates = true;
});

var app = builder.Build();
await app.InitializeTranslationToolsClientAsync();
```

Use generated translations and the runtime client:

```csharp
using System.Globalization;
using mvdmio.TranslationTools.Client;

var client = app.Services.GetRequiredService<ITranslationToolsClient>();

var generated = Localizations.Button_Save;
var dynamicValue = await Localizations.GetAsync("Button.Save");
var item = await client.GetAsync(Localizations.Keys.Button_Save, new CultureInfo("en"));
var locale = await client.GetLocaleAsync(new CultureInfo("en"));
```

Package docs: `src/mvdmio.TranslationTools.Client/Readme.md`

### `mvdmio.TranslationTools.Tool`

Use this .NET tool when you want to sync local `.resx` files with TranslationTools.

Install:

```bash
dotnet tool install --global mvdmio.TranslationTools.Tool
```

Command name: `translations`

Basic workflow:

```bash
translations init
translations pull
translations push
```

Destructive sync options:

```bash
translations pull --prune
translations push --prune
```

Example config file:

```yaml
apiKey: project-api-key
defaultLocale: en
```

- `translations init` creates `.mvdmio-translations.yml`
- `translations pull` downloads remote translations into local `.resx` files
- `translations push` uploads local `.resx` values to TranslationTools
- `translations pull --prune` also deletes local `.resx` files and entries that no longer exist remotely
- `translations push --prune` also deletes remote translations that no longer exist in local `.resx` files

`translations pull` remains project-scoped. It only writes `.resx` files for the current `.csproj` name and skips origins for other projects.

Package docs: `src/mvdmio.TranslationTools.Tool/README.md`

## Repository contents

- `src/mvdmio.TranslationTools.Client` - application client library
- `src/mvdmio.TranslationTools.Client.SourceGenerator` - source generator shipped with the client package
- `src/mvdmio.TranslationTools.Tool` - command-line sync tool
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit` - client unit tests
- `test/TranslationTools/mvdmio.TranslationTools.Tool.Tests.Unit` - tool unit tests

## Typical usage

1. Add `mvdmio.TranslationTools.Client` to your application.
2. Keep your neutral and localized `.resx` files in the project.
3. Install `mvdmio.TranslationTools.Tool` if you want to pull or push translations from the command line.
4. Run `translations pull` to refresh local resources from TranslationTools.
5. Use generated localization classes or `ITranslationToolsClient` in application code.

## Build

```bash
dotnet build mvdmio.TranslationTools.Client.slnx
```

## Test

```bash
dotnet test mvdmio.TranslationTools.Client.slnx
```
