using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal sealed class GlobalPlaceholderResolver : IGlobalPlaceholderResolver
{
   private readonly GlobalPlaceholderRegistry _registry;
   private readonly Func<IServiceProvider?> _scopeAccessor;

   public GlobalPlaceholderResolver(GlobalPlaceholderRegistry registry, Func<IServiceProvider?>? scopeAccessor = null)
   {
      _registry = registry;
      _scopeAccessor = scopeAccessor ?? (() => AmbientScope.Current);
   }

   public static GlobalPlaceholderResolver Empty { get; } = new(GlobalPlaceholderRegistry.Empty);

   public IReadOnlyCollection<string> RegisteredNames => _registry.Names;

   public bool IsRegistered(string name) => _registry.IsRegistered(name);

   public bool TryResolve(string name, out string? value)
   {
      value = null;

      var property = _registry.GetProperty(name);
      if (property is null)
         return false;

      var serviceProvider = _scopeAccessor();
      if (serviceProvider is null || _registry.ConfigType is null)
         return false;

      try
      {
         var configuration = serviceProvider.GetService(_registry.ConfigType);
         if (configuration is null)
            return false;

         var resolved = property.GetValue(configuration);
         if (resolved is null)
            return false;

         value = resolved as string
            ?? (resolved is IFormattable formattable
               ? formattable.ToString(null, CultureInfo.InvariantCulture)
               : resolved.ToString());

         return value is not null;
      }
      catch
      {
         return false;
      }
   }
}
