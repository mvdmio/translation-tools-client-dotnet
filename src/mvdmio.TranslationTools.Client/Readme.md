# mvdmio.TranslationTools.Client

Use `mvdmio.TranslationTools.Client` in a .NET application to load translations from TranslationTools while keeping local `.resx` files available as a fallback.

## Install

```bash
dotnet add package mvdmio.TranslationTools.Client
```

## What you get

- DI registration for the TranslationTools client
- runtime translation lookups through `ITranslationToolsClient`
- generated strongly typed localization classes from neutral `.resx` files
- optional live updates for runtime translations
- fallback to local `.resx` resources

## Configure

Register the client during startup:

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

Common options:

- `ApiKey`: your TranslationTools project API key
- `DefaultLocale`: locale used when no specific locale is resolved
- `SupportedLocales`: locales to preload during initialization
- `EnableLiveUpdates`: enables built-in live translation updates

## Use the client

```csharp
using System.Globalization;
using mvdmio.TranslationTools.Client;

var client = app.Services.GetRequiredService<ITranslationToolsClient>();

var item = await client.GetAsync(Localizations.Keys.Button_Save, new CultureInfo("en"));
var locale = await client.GetLocaleAsync(new CultureInfo("en"));
```

## Use generated localizations

Add a neutral `.resx` file to your project, for example `Localizations.resx`:

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

Then use the generated members in code:

```csharp
using System.Globalization;

var text = Localizations.Button_Save;
var key = Localizations.Keys.Button_Save;
var asyncText = await Localizations.GetAsync("Button.Save");
var dutch = await Localizations.GetAsync("Button.Save", new CultureInfo("nl-NL"));
```

## Local fallback

Generated localization access works well with local `.resx` files in your project. A common workflow is:

1. Keep your neutral and localized `.resx` files in source control.
2. Use `mvdmio.TranslationTools.Tool` to pull updates from TranslationTools.
3. Initialize the client during app startup.
4. Use generated localization members or `ITranslationToolsClient` in application code.

## Live updates

Set `EnableLiveUpdates = true` if you want runtime translations to refresh automatically while the app is running.
