using System;
using System.Collections.Generic;
using System.Net.Http;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class TranslationLookupRequest
{
   public TranslationRef Translation { get; }

   public EffectiveLocale Locale { get; }

   public string? NeutralValue { get; }

   public IReadOnlyDictionary<string, string?>? LocaleValues { get; }

   public TranslationLookupRequest(TranslationRef translation, EffectiveLocale locale, string? neutralValue, IReadOnlyDictionary<string, string?>? localeValues)
   {
      Translation = translation;
      Locale = locale;
      NeutralValue = neutralValue;
      LocaleValues = localeValues;
   }

   public HttpRequestMessage ToHttpRequest(string? environment)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(Translation.Origin)}/{Uri.EscapeDataString(Locale.Name)}/{Uri.EscapeDataString(Translation.Key)}";

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

   public TranslationItemResponse LocalFallback()
   {
      string? value = null;
      if (LocaleValues is not null && LocaleValues.TryGetValue(Locale.Name, out var localValue) && !string.IsNullOrEmpty(localValue))
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
