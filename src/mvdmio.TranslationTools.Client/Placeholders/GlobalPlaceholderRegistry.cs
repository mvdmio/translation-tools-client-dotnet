using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal sealed class GlobalPlaceholderRegistry
{
   private readonly IReadOnlyDictionary<string, PropertyInfo> _propertiesByName;

   private GlobalPlaceholderRegistry(Type? configType, IReadOnlyDictionary<string, PropertyInfo> propertiesByName)
   {
      ConfigType = configType;
      _propertiesByName = propertiesByName;
   }

   public static GlobalPlaceholderRegistry Empty { get; } =
      new(configType: null, new Dictionary<string, PropertyInfo>(StringComparer.Ordinal));

   public Type? ConfigType { get; }

   public IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();

   public bool IsRegistered(string name) => _propertiesByName.ContainsKey(name);

   public PropertyInfo? GetProperty(string name) => _propertiesByName.TryGetValue(name, out var property) ? property : null;

   public static GlobalPlaceholderRegistry Build(Type configType)
   {
      ArgumentNullException.ThrowIfNull(configType);

      var properties = configType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      var byName = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
      var names = new List<string>();

      foreach (var property in properties)
      {
         var attribute = property.GetCustomAttribute<GlobalPlaceholderAttribute>();
         if (attribute is null)
            continue;

         if (property.GetMethod is null)
            throw new InvalidOperationException($"Global placeholder property '{configType.FullName}.{property.Name}' must have a getter.");

         var name = ResolveTokenName(property, attribute);

         if (byName.ContainsKey(name))
            throw new InvalidOperationException($"Global placeholder name '{name}' is declared more than once on '{configType.FullName}'.");

         byName[name] = property;
         names.Add(name);
      }

      return new GlobalPlaceholderRegistry(configType, byName) { Names = names };
   }

   internal static string ResolveTokenName(PropertyInfo property, GlobalPlaceholderAttribute attribute)
   {
      if (!string.IsNullOrWhiteSpace(attribute.Name))
         return attribute.Name!;

      return CamelCase(property.Name);
   }

   internal static string CamelCase(string value)
   {
      if (string.IsNullOrEmpty(value))
         return value;

      if (char.IsLower(value[0]))
         return value;

      return char.ToLowerInvariant(value[0]) + value.Substring(1);
   }
}
