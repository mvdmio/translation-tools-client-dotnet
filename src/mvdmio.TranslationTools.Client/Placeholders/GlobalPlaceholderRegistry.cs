using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Immutable map of declared global placeholder names to the property that produces their value.
/// Built once at startup by reflecting over a configuration type's <see cref="GlobalPlaceholderAttribute"/> properties.
/// </summary>
internal sealed class GlobalPlaceholderRegistry
{
   private readonly IReadOnlyDictionary<string, PropertyInfo> _propertiesByName;

   private GlobalPlaceholderRegistry(Type? configType, IReadOnlyDictionary<string, PropertyInfo> propertiesByName)
   {
      ConfigType = configType;
      _propertiesByName = propertiesByName;
   }

   /// <summary>
   /// Empty registry (no globals declared).
   /// </summary>
   public static GlobalPlaceholderRegistry Empty { get; } =
      new(configType: null, new Dictionary<string, PropertyInfo>(StringComparer.Ordinal));

   /// <summary>
   /// The configuration type whose properties supply the global values, or null when no globals are declared.
   /// </summary>
   public Type? ConfigType { get; }

   /// <summary>
   /// Declared global placeholder names, in declaration order.
   /// </summary>
   public IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();

   /// <summary>
   /// True when <paramref name="name"/> is a declared global.
   /// </summary>
   public bool IsRegistered(string name) => _propertiesByName.ContainsKey(name);

   /// <summary>
   /// The property that produces the value for <paramref name="name"/>, or null when not registered.
   /// </summary>
   public PropertyInfo? GetProperty(string name) => _propertiesByName.TryGetValue(name, out var property) ? property : null;

   /// <summary>
   /// Build a registry by reflecting over <paramref name="configType"/>'s instance properties bearing
   /// <see cref="GlobalPlaceholderAttribute"/>.
   /// </summary>
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

   /// <summary>
   /// Token name = attribute Name override, else the property name with its first character lowercased.
   /// </summary>
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
