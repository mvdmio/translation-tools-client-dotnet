using System;
using System.Globalization;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Options for the TranslationTools API client.
/// </summary>
public sealed class TranslationToolsClientOptions
{
   internal const string DEFAULT_BASE_URL = "https://translations.mvdm.io";

   internal string BaseUrlOverride { get; set; } = DEFAULT_BASE_URL;

   /// <summary>
   /// Default locale used when no specific locale can be resolved.
   /// </summary>
   public string DefaultLocale { get; set; } = "en";

   /// <summary>
   /// Project API key sent via the Authorization header.
   /// </summary>
   public required string ApiKey { get; set; }

   /// <summary>
   /// Locales preloaded during initialization.
   /// </summary>
   public CultureInfo[] SupportedLocales { get; set; } = [];

   /// <summary>
   /// Enable built-in WebSocket live updates.
   /// </summary>
   public bool EnableLiveUpdates { get; set; }

   /// <summary>
   /// Optional deployment environment name (e.g. "production", "staging").
   /// When set (non-blank), it is appended as the final path segment on translation
   /// pull requests and carried in the heartbeat body, so the server scopes the keys
   /// it serves to this deployment's Environment. Blank or whitespace means the unnamed
   /// Environment. A set name may use letters, digits, <c>.</c>, <c>_</c>, or <c>-</c> only,
   /// must be at most 64 characters after trim, and must not be <c>.</c> or <c>..</c>.
   /// Validated when the client is constructed. Sent trimmed and as-is; the server lowercases it.
   /// </summary>
   public string? Environment { get; set; }

   /// <summary>
   /// Enable the periodic heartbeat that reports client presence to the server.
   /// Enabled by default; set to <c>false</c> to opt out of heartbeat/client tracking.
   /// </summary>
   public bool EnableHeartbeat { get; set; } = true;

   /// <summary>
   /// Interval between heartbeat reports. Defaults to one hour.
   /// </summary>
   public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromHours(1);

   /// <summary>
   /// Placeholder substitution failure behavior. Default (<c>false</c>) is warn + degrade: an unresolved
   /// placeholder is left as its raw <c>{token}</c> and a warning is logged. When <c>true</c>, an unresolved
   /// placeholder throws <see cref="PlaceholderSubstitutionException"/> instead.
   /// </summary>
   public bool ThrowOnPlaceholderError { get; set; }

   /// <summary>
   /// Translation lookup failure behavior. Default (<c>false</c>) is warn/error + degrade: a failed
   /// single-key lookup is logged and answered with the local fallback. When <c>true</c>, a failed
   /// single-key lookup throws <see cref="TranslationLookupException"/> instead.
   /// </summary>
   public bool ThrowOnLookupError { get; set; }

   /// <summary>
   /// Upper bound on how long a single-key lookup waits for the service before it degrades to the
   /// local fallback (or throws, when <see cref="ThrowOnLookupError"/> is set). Defaults to five
   /// seconds. Applied independently of the caller's own <see cref="System.Threading.CancellationToken"/>,
   /// and independently of the <see cref="System.Net.Http.HttpClient"/>'s own timeout, which this
   /// client never modifies. Does not bound a whole-locale lookup.
   /// </summary>
   public TimeSpan LookupTimeout { get; set; } = TimeSpan.FromSeconds(5);

}
