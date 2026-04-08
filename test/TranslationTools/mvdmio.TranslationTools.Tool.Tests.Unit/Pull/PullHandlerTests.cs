using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerTests
{
   [Fact]
   public void ResolveRequest_ShouldReturnNull_WhenApiKeyIsMissing()
   {
      var request = PullHandler.ResolveRequest(
         new ToolConfiguration
         {
            ApiKey = null
         }
      );

      request.Should().BeNull();
   }

   [Fact]
   public void ResolveRequest_ShouldUseRelativeOutputDirectoryForNamespace()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Demo.csproj"),
            "<Project><PropertyGroup><RootNamespace>Demo.Root</RootNamespace></PropertyGroup></Project>"
         );

         var request = PullHandler.ResolveRequest(
            new ToolConfiguration
            {
               ConfigDirectory = projectDirectory,
               ApiKey = "api-key",
               Output = Path.Combine("Generated", "Translations.g.cs")
            }
         );

         request.Should().NotBeNull();
         request!.ProjectDirectory.Should().Be(projectDirectory);
         request.OutputPath.Should().Be(Path.GetFullPath(Path.Combine(projectDirectory, "Generated", "Translations.g.cs")));
         request.SnapshotPath.Should().Be(Path.Combine(projectDirectory, ToolConfiguration.SNAPSHOT_FILE_NAME));
         request.Namespace.Should().Be("Demo.Root.Generated");
      }
      finally
      {
         DeleteDirectory(projectDirectory);
      }
   }

   [Fact]
   public void BuildPropertyDefinitions_ShouldPreferExactPropertyNameMatch()
   {
      var definitions = PullHandler.BuildPropertyDefinitions(
         [
            CreateItem("/Localizations.resx", "Account.Created", "Created"),
            CreateItem("/Localizations.resx", "Account_Created", "Created alt")
         ],
         [
            CreateItem("/Localizations.resx", "Account.Created", "Created"),
            CreateItem("/Localizations.resx", "Account_Created", "Created alt")
         ]
      );

      definitions.Should().ContainSingle();
      definitions.Single().PropertyName.Should().Be("Account_Created");
      definitions.Single().Key.Should().Be("Account_Created");
      definitions.Single().EmitExplicitKey.Should().BeFalse();
      definitions.Single().DefaultValue.Should().Be("Created alt");
   }

   [Fact]
   public void BuildPropertyDefinitions_ShouldTrimSharedKeyPrefixAndCarryDefaultValues()
   {
      var definitions = PullHandler.BuildPropertyDefinitions(
         [CreateItem("/Shared.resx", "Errors.NotFound", "Niet gevonden")],
         [CreateItem("/Shared.resx", "Errors.NotFound", "Not found")],
         sharedKeyPrefix: "Errors"
      );

      definitions.Should().ContainSingle();
      definitions.Single().PropertyName.Should().Be("NotFound");
      definitions.Single().Key.Should().Be("Errors.NotFound");
      definitions.Single().EmitExplicitKey.Should().BeTrue();
      definitions.Single().DefaultValue.Should().Be("Not found");
   }

   [Fact]
   public void BuildPropertyDefinitions_ShouldThrow_WhenIncomingItemsContainDuplicateKeys()
   {
      var act = () => PullHandler.BuildPropertyDefinitions(
         [
            CreateItem("/Localizations.resx", "Menu.Save", "Save"),
            CreateItem("/Localizations.resx", "Menu.Save", "Save again")
         ],
         []
      );

      act.Should().Throw<ArgumentException>().WithMessage("*Duplicate translation keys: Menu.Save*");
   }

   [Fact]
   public void MergePropertyDefinitions_ShouldKeepExistingKeysAndAppendNewOnes()
   {
      var existing = new[] {
         new mvdmio.TranslationTools.Tool.Scaffolding.ManifestPropertyDefinition {
            PropertyName = "Save",
            Key = "Button.Save",
            EmitExplicitKey = true,
            DefaultValue = "Save"
         }
      };
      var incoming = new[] {
         new mvdmio.TranslationTools.Tool.Scaffolding.ManifestPropertyDefinition {
            PropertyName = "SaveRenamed",
            Key = "Button.Save",
            EmitExplicitKey = true,
            DefaultValue = "Save later"
         },
         new mvdmio.TranslationTools.Tool.Scaffolding.ManifestPropertyDefinition {
            PropertyName = "Cancel",
            Key = "Button.Cancel",
            EmitExplicitKey = true,
            DefaultValue = "Cancel"
         }
      };

      var merged = PullHandler.MergePropertyDefinitions(existing, incoming);

      merged.Select(static x => x.Key).Should().Equal("Button.Save", "Button.Cancel");
      merged.First().PropertyName.Should().Be("Save");
   }

   private static TranslationItemResponse CreateItem(string origin, string key, string? value)
   {
      return new TranslationItemResponse
      {
         Origin = origin,
         Key = key,
         Value = value
      };
   }

   private static string CreateProjectDirectory()
   {
      var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(path);
      return path;
   }

   private static void DeleteDirectory(string path)
   {
      if (Directory.Exists(path))
         Directory.Delete(path, recursive: true);
   }
}
