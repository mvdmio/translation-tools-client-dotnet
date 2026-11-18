using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal static class PlaceholderRuntime
{
   private static IGlobalPlaceholderResolver _globals = GlobalPlaceholderResolver.Empty;
   private static ILogger? _logger;

   public static bool ThrowOnError { get; private set; }

   public static IGlobalPlaceholderResolver Globals => _globals;

   public static void Configure(IGlobalPlaceholderResolver globals, bool throwOnError, ILogger? logger)
   {
      _globals = globals;
      ThrowOnError = throwOnError;
      _logger = logger;
   }

   public static void Reset()
   {
      _globals = GlobalPlaceholderResolver.Empty;
      ThrowOnError = false;
      _logger = null;
   }

   public static Action<string> Warn { get; } = message =>
   {
      _logger?.LogWarning("TranslationTools placeholder: {Message}", message);
   };

   public static string Apply(string value, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet)
   {
      return PlaceholderSubstitution.Substitute(value, bindings, _globals, knownSet, ThrowOnError, Warn);
   }
}
