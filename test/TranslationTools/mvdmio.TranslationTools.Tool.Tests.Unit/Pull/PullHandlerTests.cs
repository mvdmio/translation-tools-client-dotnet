using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using mvdmio.TranslationTools.Tool.Scaffolding;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerTests
{
   public class BuildPropertyDefinitions
   {
      [Fact]
      public void ShouldGenerateExplicitKeyWhenDerivedKeyDiffers()
      {
         var result = PullHandler.BuildPropertyDefinitions(
            [
               new TranslationItemResponse { Key = "Label_Past90Days", Value = "Past 90 days" },
               new TranslationItemResponse { Key = "Action.Save", Value = "Save" }
            ],
            [
               new TranslationItemResponse { Key = "Label_Past90Days", Value = "Past 90 days" },
               new TranslationItemResponse { Key = "Action.Save", Value = "Save" }
            ],
            TranslationKeyNaming.UnderscoreToDot
         );

         result.Should().ContainSingle(x => x.PropertyName == "Label_Past90Days" && x.EmitExplicitKey && x.Key == "Label_Past90Days");
         result.Should().ContainSingle(x => x.PropertyName == "Action_Save" && !x.EmitExplicitKey && x.Key == "Action.Save");
      }

      [Fact]
      public void ShouldThrowForDuplicateResolvedPropertyNames()
      {
         var action = () => PullHandler.BuildPropertyDefinitions(
            [
               new TranslationItemResponse { Key = "Action.Save", Value = "Save" },
               new TranslationItemResponse { Key = "Action.Save", Value = "Save again" }
            ],
            [
               new TranslationItemResponse { Key = "Action.Save", Value = "Save" },
               new TranslationItemResponse { Key = "Action.Save", Value = "Save again" }
            ],
            TranslationKeyNaming.UnderscoreToDot
         );

         action.Should().Throw<ArgumentException>();
      }

      [Fact]
      public void ShouldPreferKeyMatchingConfiguredNamingWhenLegacyAndCanonicalKeysCollide()
      {
         var result = PullHandler.BuildPropertyDefinitions(
            [
               new TranslationItemResponse { Key = "Link_Back", Value = "Back legacy" },
               new TranslationItemResponse { Key = "Link.Back", Value = "Back" }
            ],
            [
               new TranslationItemResponse { Key = "Link_Back", Value = "Back legacy" },
               new TranslationItemResponse { Key = "Link.Back", Value = "Back" }
            ],
            TranslationKeyNaming.UnderscoreToDot
         );

         result.Should().ContainSingle();
         result.Should().ContainSingle(x => x.PropertyName == "Link_Back" && x.Key == "Link.Back" && !x.EmitExplicitKey && x.DefaultValue == "Back");
      }

      [Fact]
      public void ShouldTrimSharedPrefixFromPropertyNameWhenProvided()
      {
         var result = PullHandler.BuildPropertyDefinitions(
            [
               new TranslationItemResponse { Key = "Resources.Translations.Button.Save", Value = "Save" }
            ],
            [
               new TranslationItemResponse { Key = "Resources.Translations.Button.Save", Value = "Save" }
            ],
            TranslationKeyNaming.UnderscoreToDot,
            sharedKeyPrefix: "Resources.Translations"
         );

         result.Should().ContainSingle(x => x.PropertyName == "Button_Save" && x.Key == "Resources.Translations.Button.Save" && x.EmitExplicitKey);
      }
   }

   public class ResolveRequest
   {
      [Fact]
      public void ShouldResolveFromConfigAndInferNamespace()
      {
         var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
         Directory.CreateDirectory(projectDirectory);

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project><PropertyGroup><RootNamespace>Demo.App</RootNamespace></PropertyGroup></Project>");
            var outputPath = Path.Combine(projectDirectory, "Localization", "Localizations.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var request = PullHandler.ResolveRequest(
               new ToolConfiguration {
                  ConfigDirectory = projectDirectory,
                  ApiKey = "api-key",
                  Output = Path.Combine("Localization", "Localizations.cs")
               }
            );

            request.Should().NotBeNull();
            request!.Namespace.Should().Be("Demo.App.Localization");
            request.OutputPath.Should().Be(Path.GetFullPath(outputPath));
            request.ClassName.Should().Be("Localizations");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }
   }

   public class MergePropertyDefinitions
   {
      [Fact]
      public void ShouldPreserveExistingDefaultValueAndAppendNewProperties()
      {
         var result = PullHandler.MergePropertyDefinitions(
            [
               new ManifestPropertyDefinition {
                  PropertyName = "Action_Save",
                  Key = "Action.Save",
                  EmitExplicitKey = false,
                  DefaultValue = "Opslaan"
               }
            ],
            [
               new ManifestPropertyDefinition {
                  PropertyName = "Action_Save",
                  Key = "Action.Save",
                  EmitExplicitKey = false,
                  DefaultValue = "Save"
               },
               new ManifestPropertyDefinition {
                  PropertyName = "Action_Delete",
                  Key = "Action.Delete",
                  EmitExplicitKey = false,
                  DefaultValue = "Delete"
               }
            ]
         );

         result.Should().HaveCount(2);
         result.Should().ContainSingle(x => x.Key == "Action.Save" && x.DefaultValue == "Opslaan");
         result.Should().ContainSingle(x => x.Key == "Action.Delete" && x.DefaultValue == "Delete");
      }
   }

}
