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
- `{token}` placeholder substitution, including ambient global placeholders
- optional live updates for runtime translations
- optional environment scoping and presence heartbeat
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

### Options

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `ApiKey` | `string` | _(required)_ | Your TranslationTools project API key, sent as the `Authorization` header. |
| `DefaultLocale` | `string` | `"en"` | Substituted for a lookup's locale whenever that locale's name is blank (the invariant culture) before the lookup consults the cache or calls the service, so that background jobs and other invariant-culture callers resolve to a real locale instead of a collapsed request path. Must be blank-free and recognised by the runtime; validated when the client is constructed. |
| `SupportedLocales` | `CultureInfo[]` | `[]` | Locales preloaded during initialization. When left empty, the app's `RequestLocalizationOptions` cultures are used. |
| `EnableLiveUpdates` | `bool` | `false` | Enables built-in WebSocket live translation updates. See [Live updates](#live-updates). |
| `Environment` | `string?` | `null` | Deployment environment name used to scope fetched translations. See [Environment scoping](#environment-scoping). |
| `EnableHeartbeat` | `bool` | `true` | Periodically reports client presence to the server. See [Heartbeat and client identity](#heartbeat-and-client-identity). |
| `HeartbeatInterval` | `TimeSpan` | `1 hour` | Interval between heartbeat reports. |
| `ThrowOnPlaceholderError` | `bool` | `false` | Throw `PlaceholderSubstitutionException` on an unresolved placeholder instead of degrading. See [Placeholders](#placeholders). |
| `ThrowOnLookupError` | `bool` | `false` | Throw `TranslationLookupException` from a single-key lookup instead of degrading to the local fallback. See [Local fallback](#local-fallback). |
| `LookupTimeout` | `TimeSpan` | `5 seconds` | Upper bound on how long a single-key lookup waits for the service before it degrades (or throws, with `ThrowOnLookupError`). Does not bound a whole-locale lookup, and never changes the `HttpClient`'s own timeout. |

## Use the client

```csharp
using System.Globalization;
using mvdmio.TranslationTools.Client;

var client = app.Services.GetRequiredService<ITranslationToolsClient>();

var item = await client.GetAsync(Localizations.Keys.Button_Save, new CultureInfo("en"));
var locale = await client.GetLocaleAsync(new CultureInfo("en"));
```

## Environment scoping

Set `Environment` to scope the translations this deployment fetches to a named environment, for example `production` or `staging`:

```csharp
builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.DefaultLocale = "en";
   options.Environment = "production";
});
```

When `Environment` is set (non-blank):

- it is appended as the final path segment on translation fetch requests, so the server returns only the keys that belong to this environment;
- it is included in the periodic heartbeat, so the server can track which environment each client reports from.

The value is sent trimmed and as-is; the server lowercases it, so `Production` and `production` resolve to the same environment. When `Environment` is left unset (or blank), the client fetches translations from the unnamed environment.

Use the same environment name here that `mvdmio.TranslationTools.Tool` pushes keys into (the `environment` setting in `.mvdmio-translations.yml`), so the keys your deployment requests match the keys that were declared.

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

## Placeholders

Translation values can contain `{token}` placeholders that are filled in at render time. A token name starts with a lowercase letter and continues with letters or digits (`[a-z][a-zA-Z0-9]*`). To render a literal brace, wrap it in apostrophes (`'{'`, `'}'`); a doubled apostrophe (`''`) renders a literal apostrophe.

There are two kinds of placeholders:

- **key-scoped tokens** — supplied per call; each becomes a required parameter on the generated accessor;
- **global placeholders** — declared once and resolved ambiently on every render.

### Key-scoped tokens

When a neutral `.resx` value contains tokens that are not declared globals, the source generator turns that member into a method with one `string` parameter per token, in first-seen order:

```xml
<data name="Greeting">
  <value>Hello {firstName}, you have {messageCount} new messages.</value>
</data>
```

```csharp
var text = Localizations.Greeting(firstName: "Ada", messageCount: "3");
// -> "Hello Ada, you have 3 new messages."
```

A token whose name is a C# keyword is escaped with `@` in the parameter list (a `{class}` token becomes a `@class` parameter). If two distinct tokens in one value would produce the same parameter name, the generator reports a build diagnostic instead of emitting an ambiguous method.

### Global placeholders

Globals are values that apply across many translations — the signed-in user, the application name, the current tenant — that you do not want to pass on every call. Declare them as properties on a configuration type and mark each with `[GlobalPlaceholder]`:

```csharp
using mvdmio.TranslationTools.Client;

public sealed class TranslationGlobals
{
   private readonly IHttpContextAccessor _httpContextAccessor;

   public TranslationGlobals(IHttpContextAccessor httpContextAccessor)
   {
      _httpContextAccessor = httpContextAccessor;
   }

   // Token name defaults to the camelCased property name: {userName}.
   [GlobalPlaceholder]
   public string UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "";

   // Override the token name explicitly: {appName}.
   [GlobalPlaceholder("appName")]
   public string ApplicationName => "Contoso";
}
```

Register the type during startup:

```csharp
builder.Services.AddTranslationToolsGlobalPlaceholders<TranslationGlobals>();
```

The type is registered as a scoped service and resolved per request through the ambient request scope (`HttpContext.RequestServices`), so globals can read request-specific state such as the current user. The declared global names are pushed to the server when the client initializes. Tokens that match a declared global are filled automatically and do **not** appear as generated method parameters.

Non-string global values (for example `int`, `decimal`, `DateTime`) are formatted with the invariant culture so they render identically across requests and platforms.

### Resolution order

For each token in a value, the client resolves in this order:

1. a per-call binding (a generated method parameter, or `SetPlaceholder(...)`) — a binding shadows a global of the same name;
2. a registered global, resolved from the current request scope;
3. otherwise the token is unresolved (see [Error handling](#error-handling)).

### Dynamic keys

For keys looked up by string at runtime rather than through a generated member, use the fluent builder:

```csharp
using System.Globalization;

var text = Translations.WithPlaceholders(Localizations.Keys.Greeting)
   .SetPlaceholder("firstName", "Ada")
   .SetPlaceholder("messageCount", "3")
   .ForLocale(new CultureInfo("nl-NL")) // optional; defaults to CurrentUICulture
   .Render();                           // or await RenderAsync()
```

On this path the client cannot know which tokens are key-scoped, so any token that is neither bound nor a registered global is treated as unresolved.

### Error handling

By default an unresolved placeholder degrades gracefully: the raw `{token}` is left in the string and a warning is logged. A token is unresolved when no value is supplied for a key-scoped token, or when a global cannot be resolved (used outside a request scope, the property getter threw, or the value was null). Supplying a binding for a token that does not appear in the value also logs a warning.

Set `ThrowOnPlaceholderError = true` to throw `PlaceholderSubstitutionException` on any of these cases instead:

```csharp
builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.ThrowOnPlaceholderError = true;
});
```

A value fetched at runtime (for example after a [live update](#live-updates)) may contain a token that did not exist in the neutral `.resx` at build time. Through a generated accessor such an unknown token is left untouched as a literal `{token}` without warning, so server-side edits never break a render.

## Per-locale seeding from sibling `.resx` files

When the source generator finds locale-specific sibling files next to a neutral `.resx` (for example `Localizations.resx`, `Localizations.nl.resx`, `Localizations.de.resx`), it embeds the per-locale values in the generated class.

When generated members hit the API for a key the server does not yet have, the client sends every known locale value via the `localeValues` query parameter. The server seeds any missing locale rows from those values, so all locales become populated on first access instead of only the requested locale.

Existing non-empty translations on the server are never overwritten by this seeding path.

## Local fallback

Generated localization access works well with local `.resx` files in your project. A common workflow is:

1. Keep your neutral and localized `.resx` files in source control.
2. Use `mvdmio.TranslationTools.Tool` to pull updates from TranslationTools.
3. Initialize the client during app startup.
4. Use generated localization members or `ITranslationToolsClient` in application code.

## Live updates

Set `EnableLiveUpdates = true` if you want runtime translations to refresh automatically while the app is running.

## Heartbeat and client identity

When `EnableHeartbeat` is `true` (the default), the client periodically reports its presence to the server so deployments can be tracked. The first heartbeat is sent during initialization and then repeats every `HeartbeatInterval` (one hour by default). Each report carries a stable client id, the configured `Environment`, the platform, and the client version.

Heartbeats are best-effort: a failed report is logged and retried on the next tick, and never surfaces as an exception in your application.

The client id is a GUID generated once and persisted to `{LocalApplicationData}/mvdmio/TranslationTools/client-id` so the same deployment is recognized across restarts. If that file cannot be read or written, a fresh in-memory id is used instead. To control where the id is stored, register your own `IClientIdStore` before calling `AddTranslationToolsClient`.

Opt out of heartbeat and client tracking entirely with `EnableHeartbeat = false`, or change the cadence with `HeartbeatInterval`:

```csharp
builder.Services.AddTranslationToolsClient(options => {
   options.ApiKey = "project-api-key";
   options.EnableHeartbeat = false;        // disable presence reporting
   options.HeartbeatInterval = TimeSpan.FromMinutes(15); // or tune the cadence
});
```
