using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

internal static class TranslationToolsLiveUpdateMessageProcessor
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
      PropertyNameCaseInsensitive = true
   };

   public static async Task ProcessAsync(ITranslationToolsClient client, string payload, CancellationToken cancellationToken)
   {
      ArgumentNullException.ThrowIfNull(client);

      if (string.IsNullOrWhiteSpace(payload))
         return;

      TranslationToolsLiveUpdateMessage? message;

      try
      {
         message = JsonSerializer.Deserialize<TranslationToolsLiveUpdateMessage>(payload, SerializerOptions);
      }
      catch (JsonException)
      {
         return;
      }

      if (message?.Type is not "translation-updated")
         return;

      if (string.IsNullOrWhiteSpace(message.Locale) || string.IsNullOrWhiteSpace(message.Key))
         return;

      try
      {
         await client.ApplyUpdateAsync(
            new TranslationItemResponse {
               Key = message.Key,
               Value = message.Value
            },
            CultureInfo.GetCultureInfo(message.Locale),
            cancellationToken
         );
      }
      catch (CultureNotFoundException)
      {
      }
      catch (ArgumentException)
      {
      }
   }
}
