using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

internal static class TranslationToolsLiveUpdateMessageProcessor
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
   {
      PropertyNameCaseInsensitive = true
   };

   public static async Task ProcessAsync(ITranslationToolsClient client, string payload, CancellationToken cancellationToken)
   {
      await ProcessAsync(client, payload, logger: null, cancellationToken);
   }

   public static async Task ProcessAsync(ITranslationToolsClient client, string payload, ILogger? logger, CancellationToken cancellationToken)
   {
      ArgumentNullException.ThrowIfNull(client);

      if (string.IsNullOrWhiteSpace(payload))
      {
         logger?.LogDebug("Ignoring empty TranslationTools live update payload.");
         return;
      }

      TranslationToolsLiveUpdateMessage? message;

      try
      {
         message = JsonSerializer.Deserialize<TranslationToolsLiveUpdateMessage>(payload, SerializerOptions);
      }
      catch (JsonException exception)
      {
         logger?.LogWarning(exception, "Ignoring invalid TranslationTools live update payload.");
         return;
      }

      if (message?.Type is not "translation-updated")
      {
         logger?.LogDebug("Ignoring TranslationTools live update message of type {MessageType}.", message?.Type ?? "<null>");
         return;
      }

      var origin = string.IsNullOrWhiteSpace(message.Origin) ? "/Localizations.resx" : message.Origin;

      if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(message.Locale) || string.IsNullOrWhiteSpace(message.Key))
      {
          logger?.LogWarning("Ignoring translation-updated message missing origin, locale, or key.");
          return;
      }

      try
      {
           logger?.LogDebug("Applying TranslationTools live update for {Locale} {Key}.", message.Locale, message.Key);
           await client.ApplyUpdateAsync(
            new TranslationRef(origin, message.Key),
             message.Value,
             CultureInfo.GetCultureInfo(message.Locale),
             cancellationToken
          );

         logger?.LogDebug("Applied TranslationTools live update for {Locale} {Key}.", message.Locale, message.Key);
       }
      catch (CultureNotFoundException exception)
      {
         logger?.LogWarning(exception, "Ignoring TranslationTools live update for unknown locale {Locale}.", message.Locale);
      }
      catch (ArgumentException exception)
      {
         logger?.LogWarning(exception, "Ignoring TranslationTools live update for invalid identity {Origin} {Key}.", origin, message.Key);
      }
   }
}
