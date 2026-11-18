using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace mvdmio.TranslationTools.Lint;

internal static class BuildProperties
{
   public static bool IsTrue(AnalyzerOptions options, string property)
   {
      return options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property." + property, out var value)
             && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
   }
}
