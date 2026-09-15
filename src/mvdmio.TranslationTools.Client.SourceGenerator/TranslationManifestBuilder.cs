using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationManifestBuilder
{
   public static ImmutableArray<string> ResolveGlobalNames(GeneratorAttributeSyntaxContext context)
   {
      var builder = ImmutableArray.CreateBuilder<string>();

      if (context.TargetSymbol is not IPropertySymbol property)
         return builder.ToImmutable();

      foreach (var attribute in context.Attributes)
      {
         string? overrideName = null;
         if (attribute.ConstructorArguments.Length > 0)
         {
            var arg = attribute.ConstructorArguments[0];
            if (arg.Value is string s && !string.IsNullOrWhiteSpace(s))
               overrideName = s;
         }

         builder.Add(!string.IsNullOrWhiteSpace(overrideName) ? overrideName! : CamelCase(property.Name));
      }

      return builder.ToImmutable();
   }

   public static TranslationManifestBuildResult Build(
      ImmutableArray<AdditionalText> files,
      GeneratorOptions options,
      ImmutableArray<string> declaredGlobals,
      CancellationToken cancellationToken)
   {
      if (files.IsDefaultOrEmpty)
         return new TranslationManifestBuildResult();

      var globalNames = new HashSet<string>(declaredGlobals.IsDefault ? Enumerable.Empty<string>() : declaredGlobals, StringComparer.Ordinal);
      var declaredGlobalsOrdered = declaredGlobals.IsDefault
         ? ImmutableArray<string>.Empty
         : declaredGlobals.Distinct(StringComparer.Ordinal).ToImmutableArray();

      // Group files by base resx (neutral) path. Locale-suffixed files share the same base file name.
      var groups = new Dictionary<string, GroupBuilder>(StringComparer.OrdinalIgnoreCase);

      foreach (var file in files)
      {
         cancellationToken.ThrowIfCancellationRequested();

         var directory = TranslationManifestPaths.GetDirectoryName(TranslationManifestPaths.NormalizePath(file.Path));
         var fileName = TranslationManifestPaths.GetFileName(file.Path);
         string baseFileName;
         string? localeSuffix;

         if (TranslationManifestPaths.TryGetLocaleSuffix(file.Path, out var trimmedFileName, out var suffix))
         {
            baseFileName = trimmedFileName!;
            localeSuffix = suffix;
         }
         else
         {
            baseFileName = fileName;
            localeSuffix = null;
         }

         var groupKey = (string.IsNullOrEmpty(directory) ? string.Empty : directory + "/") + baseFileName;

         if (!groups.TryGetValue(groupKey, out var group))
         {
            group = new GroupBuilder { GroupKey = groupKey };
            groups[groupKey] = group;
         }

         if (localeSuffix is null)
            group.NeutralFile = file;
         else
            group.LocaleFiles.Add((localeSuffix!, file));
      }

      if (!TranslationManifestPaths.IsValidProjectName(options.ProjectName))
      {
         return new TranslationManifestBuildResult
         {
            Manifests = ImmutableArray.Create(new TranslationManifestResult
            {
               Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                  TranslationGeneratorDiagnostics.InvalidProjectName,
                  Location.None,
                  options.ProjectName
               ))
            })
         };
      }

      var manifests = new List<TranslationManifestResult>(groups.Count);
      var catalogEntries = new List<TranslationCatalogEntryModel>();

      foreach (var group in groups.Values.OrderBy(static g => g.GroupKey, StringComparer.OrdinalIgnoreCase))
      {
         cancellationToken.ThrowIfCancellationRequested();

         catalogEntries.AddRange(BuildCatalogEntries(group, options, cancellationToken));

         if (group.NeutralFile is null)
            continue;

         var result = BuildManifest(group, options, globalNames, declaredGlobalsOrdered, cancellationToken);
         if (result is not null)
            manifests.Add(result);
      }

      return new TranslationManifestBuildResult
      {
         Manifests = manifests.ToImmutableArray(),
         Catalog = catalogEntries.Count == 0
            ? null
            : new TranslationCatalogModel
            {
               Entries = catalogEntries
                  .OrderBy(static e => e.Origin, StringComparer.OrdinalIgnoreCase)
                  .ThenBy(static e => e.Key, StringComparer.Ordinal)
                  .ToImmutableArray()
            }
      };
   }

   private static IEnumerable<TranslationCatalogEntryModel> BuildCatalogEntries(
      GroupBuilder group,
      GeneratorOptions options,
      CancellationToken cancellationToken)
   {
      var pathForOrigin = group.NeutralFile?.Path ?? group.LocaleFiles.FirstOrDefault().File?.Path;
      if (string.IsNullOrWhiteSpace(pathForOrigin))
         yield break;

      var relativePath = TranslationManifestPaths.BuildProjectRelativePath(pathForOrigin!, options.ProjectDirectory);
      var origin = TranslationManifestPaths.BuildOrigin(options.ProjectName, relativePath);

      var neutralByKey = new Dictionary<string, string?>(StringComparer.Ordinal);
      if (group.NeutralFile is not null)
      {
         var neutralText = group.NeutralFile.GetText(cancellationToken)?.ToString();
         if (!string.IsNullOrWhiteSpace(neutralText))
         {
            foreach (var entry in ReadResxEntries(neutralText!))
               neutralByKey[entry.Key] = entry.Value;
         }
      }

      var localeValuesByKey = ReadLocaleValuesByKey(group, cancellationToken);

      var keys = neutralByKey.Keys
         .Concat(localeValuesByKey.Keys)
         .Distinct(StringComparer.Ordinal)
         .OrderBy(static key => key, StringComparer.Ordinal);

      foreach (var key in keys)
      {
         neutralByKey.TryGetValue(key, out var neutralValue);
         yield return new TranslationCatalogEntryModel
         {
            Origin = origin,
            Key = key,
            NeutralValue = neutralValue,
            LocaleValues = BuildLocaleValues(key, localeValuesByKey)
         };
      }
   }

   private static TranslationManifestResult? BuildManifest(
      GroupBuilder group,
      GeneratorOptions options,
      HashSet<string> globalNames,
      ImmutableArray<string> declaredGlobalsOrdered,
      CancellationToken cancellationToken)
   {
      var neutralFile = group.NeutralFile!;
      var text = neutralFile.GetText(cancellationToken)?.ToString();
      if (string.IsNullOrWhiteSpace(text))
         return null;

      var entries = ReadResxEntries(text!);
      if (entries.Count == 0)
         return null;

      var localeValuesByKey = ReadLocaleValuesByKey(group, cancellationToken);

      var relativePath = TranslationManifestPaths.BuildProjectRelativePath(neutralFile.Path, options.ProjectDirectory);
      var origin = TranslationManifestPaths.BuildOrigin(options.ProjectName, relativePath);
      var typeName = TranslationManifestPaths.BuildTypeName(neutralFile.Path);
      var @namespace = TranslationManifestPaths.BuildNamespace(relativePath, options.RootNamespace);

      var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
      var properties = ImmutableArray.CreateBuilder<TranslationManifestPropertyModel>();

      foreach (var entry in entries.GroupBy(e => TranslationManifestPaths.SanitizeIdentifier(e.Key), StringComparer.Ordinal).Select(g => g.First()))
      {
         // All tokens in the default-locale value, first-seen order.
         var allTokens = PlaceholderTokenParser.TokenNames(entry.Value);

         // Key-scoped tokens = value tokens excluding declared globals (these become required parameters).
         var keyScopedTokens = allTokens
            .Where(token => !globalNames.Contains(token))
            .ToImmutableArray();

         if (!TryDetectParameterCollision(entry.Key, keyScopedTokens, diagnostics))
            continue;

         properties.Add(new TranslationManifestPropertyModel
         {
            Name = TranslationManifestPaths.SanitizeIdentifier(entry.Key),
            Key = entry.Key,
            DefaultValue = entry.Value,
            LocaleValues = BuildLocaleValues(entry.Key, localeValuesByKey),
            Tokens = keyScopedTokens,
            HasTokens = allTokens.Count > 0
         });
      }

      return new TranslationManifestResult
      {
         Diagnostics = diagnostics.ToImmutable(),
         Model = new TranslationManifestModel
         {
            Namespace = @namespace,
            TypeName = typeName,
            Origin = origin,
            Accessibility = "public",
            DeclaredGlobalNames = declaredGlobalsOrdered,
            Properties = properties.ToImmutable()
         }
      };
   }

   /// <summary>
   /// Detect distinct tokens in one key whose generated parameter identifiers coalesce. Token grammar yields
   /// valid identifiers, so collisions are rare (keyword escaping is distinct), but guard defensively.
   /// </summary>
   private static bool TryDetectParameterCollision(string key, ImmutableArray<string> tokens, ImmutableArray<Diagnostic>.Builder diagnostics)
   {
      var collision = PlaceholderParameterNaming.FindCollision(tokens);
      if (collision is null)
         return true;

      diagnostics.Add(Diagnostic.Create(
         TranslationGeneratorDiagnostics.CollidingPlaceholderParameter,
         Location.None,
         collision.Value.Parameter,
         collision.Value.First,
         collision.Value.Second,
         key
      ));
      return false;
   }

   private static Dictionary<string, Dictionary<string, string>> ReadLocaleValuesByKey(GroupBuilder group, CancellationToken cancellationToken)
   {
      var localeValuesByKey = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
      foreach (var (locale, file) in group.LocaleFiles)
      {
         var localeText = file.GetText(cancellationToken)?.ToString();
         if (string.IsNullOrWhiteSpace(localeText))
            continue;

         foreach (var entry in ReadResxEntries(localeText!))
         {
            if (string.IsNullOrEmpty(entry.Value))
               continue;

            if (!localeValuesByKey.TryGetValue(entry.Key, out var perLocale))
            {
               perLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
               localeValuesByKey[entry.Key] = perLocale;
            }

            perLocale[locale] = entry.Value!;
         }
      }

      return localeValuesByKey;
   }

   private static ImmutableArray<TranslationManifestLocaleValueModel> BuildLocaleValues(string key, Dictionary<string, Dictionary<string, string>> localeValuesByKey)
   {
      var result = ImmutableArray.CreateBuilder<TranslationManifestLocaleValueModel>();

      if (localeValuesByKey.TryGetValue(key, out var perLocale))
      {
         foreach (var pair in perLocale.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
            result.Add(new TranslationManifestLocaleValueModel { Locale = pair.Key, Value = pair.Value });
      }

      return result.ToImmutable();
   }

   private static List<(string Key, string? Value)> ReadResxEntries(string text)
   {
      var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
      return document.Root?
         .Elements("data")
         .Select(static element => (
            Key: ((string?)element.Attribute("name") ?? string.Empty).Trim(),
            Value: NormalizeValue(element.Element("value")?.Value)
         ))
         .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
         .ToList() ?? new List<(string Key, string? Value)>();
   }

   private static string CamelCase(string value)
   {
      if (string.IsNullOrEmpty(value))
         return value;

      if (char.IsLower(value[0]))
         return value;

      return char.ToLowerInvariant(value[0]) + value.Substring(1);
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }

   internal sealed class GeneratorOptions
   {
      public string ProjectDirectory { get; set; } = string.Empty;
      public string ProjectName { get; set; } = string.Empty;
      public string RootNamespace { get; set; } = string.Empty;
   }

   private sealed class GroupBuilder
   {
      public string GroupKey { get; set; } = string.Empty;
      public AdditionalText? NeutralFile { get; set; }
      public List<(string Locale, AdditionalText File)> LocaleFiles { get; } = new();
   }
}
