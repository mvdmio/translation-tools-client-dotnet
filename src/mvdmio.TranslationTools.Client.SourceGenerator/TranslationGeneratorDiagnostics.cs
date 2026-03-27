using Microsoft.CodeAnalysis;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationGeneratorDiagnostics
{
   public static readonly DiagnosticDescriptor InvalidTargetType = new(
      id: "TTCLIENTGEN001",
      title: "Invalid translations target type",
      messageFormat: "Type '{0}' must be a top-level partial class to use [Translations]",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor MissingManifest = new(
      id: "TTCLIENTGEN002",
      title: "Missing translation properties",
      messageFormat: "Type '{0}' must contain at least one static partial string property",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor InvalidManifestProperty = new(
      id: "TTCLIENTGEN003",
      title: "Invalid translation property",
      messageFormat: "Property '{0}' on type '{1}' must be a static partial get-only string property",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor ConflictingMemberName = new(
      id: "TTCLIENTGEN004",
      title: "Conflicting generated member",
      messageFormat: "Type '{0}' already contains a member named '{1}', which is required by the translations generator",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );
}
