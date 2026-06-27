using System;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Placeholders;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class GlobalPlaceholderResolverTests
{
   [Fact]
   public void Build_DerivesCamelCaseNamesAndHonorsOverride()
   {
      var registry = GlobalPlaceholderRegistry.Build(typeof(SampleGlobals));

      registry.Names.Should().Equal("userName", "company");
      registry.IsRegistered("userName").Should().BeTrue();
      registry.IsRegistered("company").Should().BeTrue();
      registry.IsRegistered("companyName").Should().BeFalse();
   }

   [Fact]
   public void Resolve_ThroughAmbientScope_ReturnsValue()
   {
      var registry = GlobalPlaceholderRegistry.Build(typeof(SampleGlobals));
      var services = new ServiceCollection();
      services.AddScoped(_ => new SampleGlobals { UserNameValue = "Bob" });
      using var provider = services.BuildServiceProvider();

      var resolver = new GlobalPlaceholderResolver(registry, () => provider);

      resolver.TryResolve("userName", out var value).Should().BeTrue();
      value.Should().Be("Bob");
   }

   [Fact]
   public void Resolve_OutsideScope_Degrades()
   {
      var registry = GlobalPlaceholderRegistry.Build(typeof(SampleGlobals));
      var resolver = new GlobalPlaceholderResolver(registry, () => null);

      resolver.TryResolve("userName", out var value).Should().BeFalse();
      value.Should().BeNull();
   }

   [Fact]
   public void Resolve_WhenPropertyThrows_Degrades()
   {
      var registry = GlobalPlaceholderRegistry.Build(typeof(ThrowingGlobals));
      var services = new ServiceCollection();
      services.AddScoped(_ => new ThrowingGlobals());
      using var provider = services.BuildServiceProvider();

      var resolver = new GlobalPlaceholderResolver(registry, () => provider);

      resolver.TryResolve("boom", out var value).Should().BeFalse();
      value.Should().BeNull();
   }

   [Fact]
   public void AddGlobalPlaceholders_RegistersConfigScopedAndBuildsRegistry()
   {
      var services = new ServiceCollection();
      services.AddTranslationToolsClient(options => options.ApiKey = "k");
      services.AddTranslationToolsGlobalPlaceholders<SampleGlobals>();

      using var provider = services.BuildServiceProvider();

      var registry = provider.GetRequiredService<GlobalPlaceholderRegistry>();
      registry.Names.Should().Equal("userName", "company");

      using var scope = provider.CreateScope();
      scope.ServiceProvider.GetService<SampleGlobals>().Should().NotBeNull();
   }

   private sealed class SampleGlobals
   {
      public string UserNameValue { get; set; } = "";

      [GlobalPlaceholder]
      public string UserName => UserNameValue;

      [GlobalPlaceholder("company")]
      public string CompanyName => "Acme";
   }

   private sealed class ThrowingGlobals
   {
      [GlobalPlaceholder]
      public string Boom => throw new InvalidOperationException("nope");
   }
}
