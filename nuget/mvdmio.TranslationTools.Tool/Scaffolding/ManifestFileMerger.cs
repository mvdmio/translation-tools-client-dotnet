using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mvdmio.TranslationTools.Client;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestFileMerger
{
   private readonly ManifestFileParser _manifestFileParser;
   private readonly ManifestFileBuilder _manifestFileBuilder;

   public ManifestFileMerger(ManifestFileParser manifestFileParser, ManifestFileBuilder manifestFileBuilder)
   {
      _manifestFileParser = manifestFileParser;
      _manifestFileBuilder = manifestFileBuilder;
   }

   public ManifestMergeResult Merge(
      string existingContent,
      string className,
      TranslationKeyNaming keyNaming,
      IReadOnlyCollection<ManifestPropertyDefinition> incomingDefinitions
   )
   {
      var parseResult = _manifestFileParser.ParseDocument(existingContent, className, keyNaming);
      if (parseResult.ClassDeclaration is null)
      {
         return new ManifestMergeResult {
            Content = existingContent,
            AddedPropertyCount = 0,
            ClassDeclaration = null
         };
      }

      var existingKeys = parseResult.Properties.ToDictionary(static x => x.Key, static x => x, StringComparer.Ordinal);
      var existingPropertyNames = parseResult.Properties.Select(static x => x.PropertyName).ToHashSet(StringComparer.Ordinal);
      var missingDefinitions = incomingDefinitions.Where(x => !existingKeys.ContainsKey(x.Key) && !existingPropertyNames.Contains(x.PropertyName)).ToArray();
      if (missingDefinitions.Length == 0)
      {
         return new ManifestMergeResult {
            Content = existingContent,
            AddedPropertyCount = 0,
            ClassDeclaration = parseResult.ClassDeclaration
         };
      }

      var newline = existingContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
      var indentation = ResolveIndentation(parseResult.ClassDeclaration);
      var memberSeparator = ResolveMemberSeparator(existingContent, parseResult.ClassDeclaration, newline);
      var closeBracePosition = parseResult.ClassDeclaration.CloseBraceToken.FullSpan.Start;
      var insertPrefix = parseResult.ClassDeclaration.Members.Count > 0 ? memberSeparator : newline;
      var propertyBlocks = string.Join(memberSeparator, missingDefinitions.Select(x => _manifestFileBuilder.BuildPropertyBlock(x, indentation, newline)));
      var insertedText = insertPrefix + propertyBlocks;

      return new ManifestMergeResult {
         Content = existingContent.Insert(closeBracePosition, insertedText),
         AddedPropertyCount = missingDefinitions.Length,
         ClassDeclaration = parseResult.ClassDeclaration
      };
   }

   private static string ResolveIndentation(ClassDeclarationSyntax classDeclaration)
   {
      var existingProperty = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault();
      if (existingProperty is not null)
      {
         var trivia = existingProperty.GetLeadingTrivia().ToFullString();
         var newlineIndex = trivia.LastIndexOf('\n');
         return newlineIndex >= 0 ? trivia[(newlineIndex + 1)..] : trivia;
      }

      return "   ";
   }

   private static string ResolveMemberSeparator(string content, ClassDeclarationSyntax classDeclaration, string newline)
   {
      var members = classDeclaration.Members;

      if (members.Count >= 2)
      {
         for (var index = members.Count - 1; index > 0; index--)
         {
            var previousMember = members[index - 1];
            var currentMember = members[index];
            var separator = BuildLineBasedSeparator(classDeclaration.SyntaxTree, previousMember.Span.End, currentMember.FullSpan.Start, newline);
            var resolved = NormalizeSeparator(separator, newline);
            if (resolved is not null)
               return resolved;
         }
      }

      if (members.Count == 1)
      {
         var singleMember = members[0];
         var separator = BuildLineBasedSeparator(classDeclaration.SyntaxTree, classDeclaration.OpenBraceToken.Span.End, singleMember.FullSpan.Start, newline);
         var resolved = NormalizeSeparator(separator, newline);
         if (resolved is not null)
            return resolved;
      }

      return newline + newline;
   }

   private static string? NormalizeSeparator(string separator, string newline)
   {
      if (string.IsNullOrEmpty(separator))
         return null;

      if (separator.All(char.IsWhiteSpace))
      {
         if (!separator.Contains('\n'))
            return null;

         return separator.TrimEnd(' ', '\t');
      }

      var newlineCount = separator.Count(static character => character == '\n');
      if (newlineCount == 0)
         return null;

      return string.Concat(Enumerable.Repeat(newline, newlineCount));
   }

   private static string BuildLineBasedSeparator(Microsoft.CodeAnalysis.SyntaxTree syntaxTree, int start, int end, string newline)
   {
      var text = syntaxTree.GetText();
      var startLine = text.Lines.GetLineFromPosition(start).LineNumber;
      var endLine = text.Lines.GetLineFromPosition(end).LineNumber;
      var lineDelta = endLine - startLine;

      return lineDelta <= 0
         ? string.Empty
         : string.Concat(Enumerable.Repeat(newline, lineDelta));
   }
}
