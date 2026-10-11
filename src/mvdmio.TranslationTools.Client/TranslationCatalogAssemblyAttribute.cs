using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Marks an assembly that carries generated translation catalog data. The source generator adds it
/// beside the module initializer that registers the assembly's keys with <see cref="TranslationCatalog"/>,
/// so the client can find the assembly and run that initializer before it sends the Declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TranslationCatalogAssemblyAttribute : Attribute
{
}
