using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using mvdmio.TranslationTools.Client.Placeholders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

[Collection("PlaceholderRuntime")]
public sealed class PlaceholderRuntimeWiringTests : IDisposable
{
   public PlaceholderRuntimeWiringTests()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   public void Dispose()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   [Fact]
   public void GetWithPlaceholders_AppliesBindings()
   {
      Translations.SetClient(new EchoClient("Hello {userName}!"));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var result = Translations.GetWithPlaceholders(
         new TranslationRef("o:/r.resx", "k"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?> { ["userName"] = "Bob" },
         knownSet: new[] { "userName" });

      result.Should().Be("Hello Bob!");
   }

   [Fact]
   public void GetWithPlaceholders_ResolvesGlobalThroughAmbientScope()
   {
      Translations.SetClient(new EchoClient("Welcome to {companyName}"));

      var registry = GlobalPlaceholderRegistry.Build(typeof(Globals));
      var services = new ServiceCollection();
      services.AddScoped(_ => new Globals { Company = "Acme" });
      using var provider = services.BuildServiceProvider();
      using var scope = provider.CreateScope();

      AmbientScope.SetAccessor(() => scope.ServiceProvider);
      PlaceholderRuntime.Configure(new GlobalPlaceholderResolver(registry), throwOnError: false, logger: null);

      var result = Translations.GetWithPlaceholders(
         new TranslationRef("o:/r.resx", "k"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?>(),
         knownSet: new[] { "companyName" });

      result.Should().Be("Welcome to Acme");
   }

   [Fact]
   public void GetWithPlaceholders_BindingShadowsGlobal()
   {
      Translations.SetClient(new EchoClient("Welcome to {companyName}"));

      var registry = GlobalPlaceholderRegistry.Build(typeof(Globals));
      var services = new ServiceCollection();
      services.AddScoped(_ => new Globals { Company = "Acme" });
      using var provider = services.BuildServiceProvider();
      using var scope = provider.CreateScope();

      AmbientScope.SetAccessor(() => scope.ServiceProvider);
      PlaceholderRuntime.Configure(new GlobalPlaceholderResolver(registry), throwOnError: false, logger: null);

      var result = Translations.GetWithPlaceholders(
         new TranslationRef("o:/r.resx", "k"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?> { ["companyName"] = "Local" },
         knownSet: new[] { "companyName" });

      result.Should().Be("Welcome to Local");
   }

   [Fact]
   public void GetWithPlaceholders_OutsideScope_GlobalDegrades()
   {
      Translations.SetClient(new EchoClient("Welcome to {companyName}"));

      var registry = GlobalPlaceholderRegistry.Build(typeof(Globals));
      AmbientScope.SetAccessor(() => null);
      PlaceholderRuntime.Configure(new GlobalPlaceholderResolver(registry), throwOnError: false, logger: null);

      var result = Translations.GetWithPlaceholders(
         new TranslationRef("o:/r.resx", "k"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?>(),
         knownSet: new[] { "companyName" });

      result.Should().Be("Welcome to {companyName}");
   }

   [Fact]
   public void GetWithPlaceholders_ThrowMode_Throws()
   {
      Translations.SetClient(new EchoClient("Hi {userName}"));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: true, logger: null);

      Action act = () => Translations.GetWithPlaceholders(
         new TranslationRef("o:/r.resx", "k"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?>(),
         knownSet: new[] { "userName" });

      act.Should().Throw<PlaceholderSubstitutionException>();
   }

   [Fact]
   public void Builder_StringKeyed_RendersBindings()
   {
      Translations.SetClient(new EchoClient("Hello {userName}!"));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var result = Translations.WithPlaceholders(new TranslationRef("o:/r.resx", "k"))
         .SetPlaceholder("userName", "Bob")
         .Render();

      result.Should().Be("Hello Bob!");
   }

   private sealed class Globals
   {
      public string Company { get; set; } = "";

      [GlobalPlaceholder]
      public string CompanyName => Company;
   }

   // A client that echoes a fixed value, so substitution is observable.
   private sealed class EchoClient : ITranslationToolsClient
   {
      private readonly string _value;

      public EchoClient(string value) => _value = value;

      public Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default)
         => GetAsync(translation, CultureInfo.CurrentUICulture, cancellationToken);

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
         => Task.FromResult(new TranslationItemResponse { Origin = translation.Origin, Key = translation.Key, Value = _value });

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
         => GetAsync(translation, locale, cancellationToken);

      public Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
         => Task.FromResult(new TranslationLocaleSnapshot(locale.Name, new Dictionary<TranslationRef, string?>()));
   }
}
