using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Process-wide placeholder configuration, set once at startup, read by the static <see cref="Translations"/> entry points.
/// </summary>
internal static class PlaceholderRuntime
{
   private static IGlobalPlaceholderResolver _globals = GlobalPlaceholderResolver.Empty;
   private static ILogger? _logger;

   /// <summary>
   /// Whether placeholder errors throw instead of warning + degrading.
   /// </summary>
   public static bool ThrowOnError { get; private set; }

   /// <summary>
   /// The ambient global placeholder resolver.
   /// </summary>
   public static IGlobalPlaceholderResolver Globals => _globals;

   /// <summary>
   /// Configure the placeholder runtime. Called from DI startup.
   /// </summary>
   public static void Configure(IGlobalPlaceholderResolver globals, bool throwOnError, ILogger? logger)
   {
      _globals = globals;
      ThrowOnError = throwOnError;
      _logger = logger;
   }

   /// <summary>
   /// Reset to defaults. Intended for tests.
   /// </summary>
   public static void Reset()
   {
      _globals = GlobalPlaceholderResolver.Empty;
      ThrowOnError = false;
      _logger = null;
   }

   /// <summary>
   /// Warning sink that logs through the configured logger.
   /// </summary>
   public static Action<string> Warn { get; } = message =>
   {
      if (_logger is not null)
         _logger.LogWarning("TranslationTools placeholder: {Message}", message);
   };

   /// <summary>
   /// Apply substitution using the configured runtime globals/options.
   /// </summary>
   public static string Apply(string value, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet)
   {
      return PlaceholderSubstitution.Substitute(value, bindings, _globals, knownSet, ThrowOnError, Warn);
   }
}
