using System.Collections.Generic;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal interface IGlobalPlaceholderResolver
{
   IReadOnlyCollection<string> RegisteredNames { get; }

   bool IsRegistered(string name);

   bool TryResolve(string name, out string? value);
}
