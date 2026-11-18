using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;

namespace mvdmio.TranslationTools.Client.Internal;

internal readonly struct TranslationLookupFailure
{
   private const string NoValueForThisKey = "the service has no value for this key";

   private const string CouldNotAnswer = "the service could not answer";

   public LogLevel Level { get; }

   public string Reason { get; }

   public bool OpensSuppressionWindow { get; }

   private TranslationLookupFailure(LogLevel level, string reason, bool opensSuppressionWindow)
   {
      Level = level;
      Reason = reason;
      OpensSuppressionWindow = opensSuppressionWindow;
   }

   public static TranslationLookupFailure Suppressed { get; } =
      new(LogLevel.Warning, "the service is suppressed after a recent failure", opensSuppressionWindow: false);

   public static TranslationLookupFailure Classify(Exception exception)
   {
      return exception switch
      {
         OperationCanceledException => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: true),

         HttpRequestException { StatusCode: { } statusCode } => FromStatusCode(statusCode),

         HttpRequestException => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: true),

         _ => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: false)
      };
   }

   private static TranslationLookupFailure FromStatusCode(HttpStatusCode statusCode)
   {
      if (statusCode == HttpStatusCode.Unauthorized)
         return new TranslationLookupFailure(LogLevel.Error, CouldNotAnswer, opensSuppressionWindow: false);

      if (statusCode == HttpStatusCode.NotFound)
         return new TranslationLookupFailure(LogLevel.Warning, NoValueForThisKey, opensSuppressionWindow: false);

      return new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: (int)statusCode >= 500);
   }
}
