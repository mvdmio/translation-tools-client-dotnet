using System.Collections.Generic;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Resolves global placeholder values ambiently from the current scope.
/// </summary>
internal interface IGlobalPlaceholderResolver
{
   /// <summary>
   /// All declared global placeholder names.
   /// </summary>
   IReadOnlyCollection<string> RegisteredNames { get; }

   /// <summary>
   /// True when <paramref name="name"/> is a registered global placeholder.
   /// </summary>
   bool IsRegistered(string name);

   /// <summary>
   /// Try to resolve a global placeholder value for the current ambient scope.
   /// Returns false (degrade) when outside a scope, when the resolver throws, or when the value is null.
   /// </summary>
   bool TryResolve(string name, out string? value);
}
