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
   /// it serves to this deployment's Environment. Sent as-is; the server lowercases it.
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

}
