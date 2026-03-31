using System;
using System.Globalization;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Options for the TranslationTools API client.
/// </summary>
public sealed class TranslationToolsClientOptions
{
   internal const string DEFAULT_BASE_URL = "https://translations.mvdm.io";

   /// <summary>
   /// Project API key sent via the Authorization header.
   /// </summary>
   public required string ApiKey { get; set; }

   /// <summary>
   /// Locales preloaded during initialization.
   /// </summary>
   public CultureInfo[] SupportedLocales { get; set; } = [];

}
