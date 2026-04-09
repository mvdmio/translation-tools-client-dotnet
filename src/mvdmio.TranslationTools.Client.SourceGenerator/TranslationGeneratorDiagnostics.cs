using Microsoft.CodeAnalysis;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationGeneratorDiagnostics
{
   public static readonly DiagnosticDescriptor ConflictingResourceSet = new(
      id: "TTCLIENTGEN001",
      title: "Conflicting resource set",
      messageFormat: "Resource set '{0}' is produced by both '{1}' and '{2}' after path normalization.",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor ConflictingPropertyName = new(
      id: "TTCLIENTGEN002",
      title: "Conflicting generated property",
      messageFormat: "Keys '{1}' and '{2}' in '{3}' both normalize to generated property '{0}'.",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor StaleDesignerFile = new(
      id: "TTCLIENTGEN003",
      title: "Stale designer file detected",
      messageFormat: "Remove stale generated designer file '{0}'. TranslationTools generates resource classes from `.resx` directly.",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );

   public static readonly DiagnosticDescriptor BuiltInGeneratorEnabled = new(
      id: "TTCLIENTGEN004",
      title: "Built-in resx generator still enabled",
      messageFormat: "Project resource '{0}' still has built-in designer generation enabled. TranslationTools owns all project `.resx` files.",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
    );

   public static readonly DiagnosticDescriptor InvalidProjectName = new(
      id: "TTCLIENTGEN005",
      title: "Invalid project name",
      messageFormat: "Project '{0}' cannot be used in translation origins. Project names must be non-empty and must not contain ':'.",
      category: "mvdmio.TranslationTools.Client",
      defaultSeverity: DiagnosticSeverity.Error,
      isEnabledByDefault: true
   );
}
