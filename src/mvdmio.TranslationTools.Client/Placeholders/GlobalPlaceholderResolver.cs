using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Resolves global placeholder values through the current ambient request scope.
/// </summary>
internal sealed class GlobalPlaceholderResolver : IGlobalPlaceholderResolver
{
   private readonly GlobalPlaceholderRegistry _registry;
   private readonly Func<IServiceProvider?> _scopeAccessor;

   public GlobalPlaceholderResolver(GlobalPlaceholderRegistry registry, Func<IServiceProvider?>? scopeAccessor = null)
   {
      _registry = registry;
      _scopeAccessor = scopeAccessor ?? (() => AmbientScope.Current);
   }

   /// <summary>
   /// A resolver with no registered globals.
   /// </summary>
   public static GlobalPlaceholderResolver Empty { get; } = new(GlobalPlaceholderRegistry.Empty);

   /// <inheritdoc />
   public IReadOnlyCollection<string> RegisteredNames => _registry.Names;

   /// <inheritdoc />
   public bool IsRegistered(string name) => _registry.IsRegistered(name);

   /// <inheritdoc />
   public bool TryResolve(string name, out string? value)
   {
      value = null;

      var property = _registry.GetProperty(name);
      if (property is null)
         return false;

      var serviceProvider = _scopeAccessor();
      if (serviceProvider is null || _registry.ConfigType is null)
         return false; // Outside a scope -> degrade.

      try
      {
         var configuration = serviceProvider.GetService(_registry.ConfigType);
         if (configuration is null)
            return false;

         var resolved = property.GetValue(configuration);
         if (resolved is null)
            return false; // Null value -> degrade.

         // Format culture-invariantly so a non-string global (decimal/int/DateTime) renders deterministically
         // and identically across requests/platforms — Phase 1 is pure string substitution, not locale-aware.
         value = resolved as string
            ?? (resolved is IFormattable formattable
               ? formattable.ToString(null, CultureInfo.InvariantCulture)
               : resolved.ToString());

         return value is not null;
      }
      catch
      {
         // Resolver threw -> degrade.
         return false;
      }
   }
}
