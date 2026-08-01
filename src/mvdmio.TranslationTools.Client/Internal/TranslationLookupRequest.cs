using System;
using System.Collections.Generic;
using System.Net.Http;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// One single-key lookup: which translation, which effective locale, and the local <c>.resx</c>
/// text the application shipped with. These four values travel together through every step of a
/// lookup, so they travel as one type.
/// </summary>
internal sealed class TranslationLookupRequest
{
   /// <summary>
   /// The translation being looked up.
   /// </summary>
   public TranslationRef Translation { get; }

   /// <summary>
   /// The locale the lookup actually runs against, after a blank locale has been replaced by the
   /// configured default.
   /// </summary>
   public string EffectiveLocale { get; }

   /// <summary>
   /// The value the translation key has in its <c>.resx</c> file with no locale suffix.
   /// </summary>
   public string? NeutralValue { get; }

   /// <summary>
   /// The application's own <c>.resx</c> text for this key, keyed by locale name.
   /// </summary>
   public IReadOnlyDictionary<string, string?>? LocaleValues { get; }

   public TranslationLookupRequest(TranslationRef translation, string effectiveLocale, string? neutralValue, IReadOnlyDictionary<string, string?>? localeValues)
   {
      Translation = translation;
      EffectiveLocale = effectiveLocale;
      NeutralValue = neutralValue;
      LocaleValues = localeValues;
   }

   /// <summary>
   /// Builds the GET the service answers. The neutral value and the locale values ride along as
   /// query parameters on every fetch, so the service is still seeded by a successful lookup.
   /// </summary>
   public HttpRequestMessage ToHttpRequest(string? environment)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(Translation.Origin)}/{Uri.EscapeDataString(EffectiveLocale)}/{Uri.EscapeDataString(Translation.Key)}";

      if (environment is not null)
         url += $"/{Uri.EscapeDataString(environment)}";

      var query = new List<string>();
      if (NeutralValue is not null)
         query.Add($"defaultValue={Uri.EscapeDataString(NeutralValue)}");

      if (LocaleValues is { Count: > 0 })
      {
         foreach (var pair in LocaleValues)
         {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrEmpty(pair.Value))
               continue;

            query.Add($"localeValues[{Uri.EscapeDataString(pair.Key)}]={Uri.EscapeDataString(pair.Value!)}");
         }
      }

      if (query.Count > 0)
         url += "?" + string.Join("&", query);

      return new HttpRequestMessage(HttpMethod.Get, url);
   }

   /// <summary>
   /// The local fallback for this lookup: the locale values entry for the effective locale, matched
   /// on its exact name, then the neutral value.
   ///
   /// The chain stops there rather than ending in the translation key. A caller using
   /// <see cref="ITranslationToolsClient"/> directly supplies neither a neutral value nor locale
   /// values, and gets a response whose <c>Value</c> is null — see
   /// <c>docs/adr/0001-a-translation-lookup-never-throws.md</c>. The generated accessors reach the
   /// key through <see cref="Translations"/>, which ends in <c>?? defaultValue ?? translation.Key</c>,
   /// so a generated property is still never null or empty.
   /// </summary>
   public TranslationItemResponse LocalFallback()
   {
      string? value = null;
      if (LocaleValues is not null && LocaleValues.TryGetValue(EffectiveLocale, out var localValue) && !string.IsNullOrEmpty(localValue))
         value = localValue;

      value ??= NeutralValue;

      return new TranslationItemResponse
      {
         Origin = Translation.Origin,
         Key = Translation.Key,
         Value = value
      };
   }
}
