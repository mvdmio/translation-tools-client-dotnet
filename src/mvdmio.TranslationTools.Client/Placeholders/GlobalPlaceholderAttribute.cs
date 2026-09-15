using JetBrains.Annotations;
using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Marks an instance property on a registered configuration type as a global placeholder source.
/// The token name is the property name camelCased (first character lowercased) unless overridden via <see cref="Name"/>.
/// </summary>
/// <example>
/// <code>
/// public sealed class TranslationGlobals
/// {
///    [GlobalPlaceholder]
///    public string UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "";
/// }
/// </code>
/// </example>
[PublicAPI]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class GlobalPlaceholderAttribute : Attribute
{
   /// <summary>
   /// Create a global placeholder declaration.
   /// </summary>
   /// <param name="name">Optional token name override. When null, the camelCased property name is used.</param>
   public GlobalPlaceholderAttribute(string? name = null)
   {
      Name = name;
   }

   /// <summary>
   /// Explicit token name override. When null, the camelCased property name is used.
   /// </summary>
   public string? Name { get; }
}
