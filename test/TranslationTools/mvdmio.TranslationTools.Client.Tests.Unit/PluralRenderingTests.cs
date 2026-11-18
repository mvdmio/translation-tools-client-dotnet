using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using mvdmio.TranslationTools.Client.Placeholders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

[Collection("PlaceholderRuntime")]
public sealed class PluralRenderingTests : IDisposable
{
   private const string CartEn = "{count, plural, =0 {Your cart is empty} one {# item in {cartName}} other {# items in {cartName}}}";
   private const string CartPl = "{count, plural, =0 {Koszyk jest pusty} one {# produkt w {cartName}} few {# produkty w {cartName}} many {# produktów w {cartName}} other {# produktu w {cartName}}}";
   private const string MissingNumberWarning = "TranslationTools placeholder: No number supplied for the Plural in this value.";

   private static readonly TranslationRef _cart = new("o:/r.resx", "Cart.ItemCount");

   private readonly FakeLogger _logger = new();

   public PluralRenderingTests()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   public void Dispose()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   [Theory]
   [InlineData(0, "Your cart is empty")]
   [InlineData(1, "1 item in Groceries")]
   [InlineData(5, "5 items in Groceries")]
   [InlineData(1000, "1,000 items in Groceries")]
   public void Render_English_PicksFormByNumber(long number, string expected)
   {
      Render("en", number, ("en", CartEn)).Should().Be(expected);
   }

   [Theory]
   [InlineData(0, "Koszyk jest pusty")]
   [InlineData(1, "1 produkt w Groceries")]
   [InlineData(2, "2 produkty w Groceries")]
   [InlineData(5, "5 produktów w Groceries")]
   [InlineData(22, "22 produkty w Groceries")]
   [InlineData(25, "25 produktów w Groceries")]
   public void Render_Polish_PicksFormByCldrCategory(long number, string expected)
   {
      Render("pl", number, ("pl", CartPl)).Should().Be(expected);
   }

   [Fact]
   public void Render_JapaneseWithEnglishText_UsesOtherForm()
   {
      Render("ja", 3, ("", CartEn)).Should().Be("3 items in Groceries");
   }

   [Fact]
   public void Render_RegionLocaleWithoutRules_UsesLanguageRules()
   {
      const string VALUE = "{count, plural, one {# um} other {# outros}}";

      Render("pt-xx", 1, ("pt-xx", VALUE)).Should().Be("1 um");
   }

   [Fact]
   public void Render_LanguageUnknownToCldr_UsesOtherForm()
   {
      const string VALUE = "{count, plural, one {# one} other {# other}}";

      Render("zz", 1, ("zz", VALUE)).Should().Be("1 other");
   }

   [Fact]
   public void Render_ValueWithoutPickedCategory_UsesOtherForm()
   {
      Render("pl", 5, ("pl", "{count, plural, one {# item} other {# items}}")).Should().Be("5 items");
   }

   [Fact]
   public void Render_ValueWithoutOtherForm_UsesLastForm()
   {
      Render("en", 7, ("en", "{count, plural, one {single} few {several}}")).Should().Be("several");
   }

   [Fact]
   public void Render_ThousandInDutch_GroupsWithDots()
   {
      Render("nl", 1000, ("nl", "{count, plural, one {# artikel} other {# artikelen}}")).Should().Be("1.000 artikelen");
   }

   [Fact]
   public void Render_NegativeNumber_PicksFormByAbsoluteValueAndPrintsSign()
   {
      Render("en", -1, ("en", "{count, plural, =1 {exactly one} one {# item} other {# items}}")).Should().Be("-1 item");
   }

   [Fact]
   public void Render_MinimumLongNumber_PrintsSign()
   {
      Render("en", long.MinValue, ("en", "{count, plural, one {# item} other {# items}}")).Should().Be("-9,223,372,036,854,775,808 items");
   }

   [Fact]
   public void Render_ArgumentTokenInForm_PrintsNumberLikeHash()
   {
      Render("en", 1000, ("en", "{count, plural, one {{count} item} other {{count} items}}")).Should().Be("1,000 items");
   }

   [Fact]
   public void Render_BlankLocale_UsesDefaultLocaleRules()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { [""] = CartPl }), defaultLocale: "pl");
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var result = Translations.WithPlaceholders(_cart)
         .ForLocale(CultureInfo.InvariantCulture)
         .SetPlaceholder("cartName", "Groceries")
         .SetPluralNumber(22)
         .Render();

      result.Should().Be("22 produkty w Groceries");
   }

   [Fact]
   public void Render_WithoutNumber_WarnsAndRendersOtherFormWithHash()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = CartEn }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("cartName", "Groceries").Render();

      result.Should().Be("# items in Groceries");
      _logger.Entries.Select(e => e.Message).Should().ContainSingle().Which.Should().Be(MissingNumberWarning);
   }

   [Fact]
   public void Render_WithoutNumberAndWithoutOtherForm_RendersLastForm()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = "{count, plural, one {single} few {several # of them}}" }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).Render();

      result.Should().Be("several # of them");
   }

   [Fact]
   public void Render_WithoutNumber_ThrowMode_ThrowsMissingNumberMessage()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = CartEn }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: true, logger: null);

      var act = () => Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("cartName", "Groceries").Render();

      act.Should().Throw<PlaceholderSubstitutionException>().WithMessage("No number supplied for the Plural in this value.");
   }

   [Fact]
   public void Render_NumberForPlainValue_IsIgnoredWithoutWarning()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = "Hello {userName}!" }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: true, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("userName", "Bob").SetPluralNumber(5).Render();

      result.Should().Be("Hello Bob!");
      _logger.Entries.Should().BeEmpty();
   }

   [Theory]
   [InlineData("{a, plural, one {# x} other {# y}}{b, plural, one {# x} other {# y}}", "{a, plural, one {# x} other {# y}}{b, plural, one {# x} other {# y}}")]
   [InlineData("{count, plural, one {{n, plural, one {# a} other {# b}}} other {# c}}", "{count, plural, one {{n, plural, one {# a} other {# b}}} other {# c}}")]
   public void Render_ValueThatIsNotAPlural_RendersAsPlainText(string value, string expected)
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = value }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: true, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPluralNumber(1).Render();

      result.Should().Be(expected);
      _logger.Entries.Should().BeEmpty();
   }

   [Fact]
   public void Render_PluralFragmentInsideOtherText_RendersAsPlainTextWithTokensSubstituted()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = "You have {count, plural, one {# item} other {# items}} in {cartName}" }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: true, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("cartName", "Groceries").SetPluralNumber(1).Render();

      result.Should().Be("You have {count, plural, one {# item} other {# items}} in Groceries");
      _logger.Entries.Should().BeEmpty();
   }

   [Fact]
   public void Render_BindingsUsedOnlyInUnchosenForm_RaiseNoWarning()
   {
      const string VALUE = "{count, plural, one {just {oneName}} other {# items in {otherName}}}";
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = VALUE }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("oneName", "x").SetPlaceholder("otherName", "y").SetPluralNumber(5).Render();

      result.Should().Be("5 items in y");
      _logger.Entries.Should().BeEmpty();
   }

   [Fact]
   public void Render_UnboundTokenInUnchosenForm_RaisesNoWarning()
   {
      const string VALUE = "{count, plural, one {just {oneName}} other {# items in {otherName}}}";
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = VALUE }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPlaceholder("otherName", "y").SetPluralNumber(5).Render();

      result.Should().Be("5 items in y");
      _logger.Entries.Should().BeEmpty();
   }

   [Fact]
   public void Render_UnboundTokenInChosenForm_StillWarnsAndKeepsToken()
   {
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = CartEn }));

      var result = Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("en")).SetPluralNumber(5).Render();

      result.Should().Be("5 items in {cartName}");
      _logger.Entries.Select(e => e.Message).Should().ContainSingle().Which.Should().Be("TranslationTools placeholder: No value supplied for placeholder '{cartName}'.");
   }

   [Fact]
   public async Task RenderAsync_WithNumber_PicksFormByNumber()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["pl"] = CartPl }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var result = await Translations.WithPlaceholders(_cart).ForLocale(new CultureInfo("pl")).SetPlaceholder("cartName", "Groceries").SetPluralNumber(22).RenderAsync(TestContext.Current.CancellationToken);

      result.Should().Be("22 produkty w Groceries");
   }

   [Fact]
   public void GetWithPlaceholders_WithNumber_PicksFormByNumber()
   {
      Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["pl"] = CartPl }));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var result = Translations.GetWithPlaceholders(
         _cart,
         new CultureInfo("pl"),
         defaultValue: null,
         localeValues: null,
         bindings: new Dictionary<string, string?> { ["cartName"] = "Groceries" },
         knownSet: new[] { "cartName" },
         pluralNumber: 25);

      result.Should().Be("25 produktów w Groceries");
   }

   [Fact]
   public async Task GetWithPlaceholdersAsync_WithNumberWithoutLocale_PicksFormByNumber()
   {
      var previousCulture = CultureInfo.CurrentUICulture;
      CultureInfo.CurrentUICulture = new CultureInfo("en");

      try
      {
         Translations.SetClient(new LocaleClient(new Dictionary<string, string> { ["en"] = CartEn }));
         PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

         var result = await Translations.GetWithPlaceholdersAsync(
            _cart,
            defaultValue: null,
            localeValues: null,
            bindings: new Dictionary<string, string?> { ["cartName"] = "Groceries" },
            knownSet: new[] { "cartName" },
            pluralNumber: 1,
            TestContext.Current.CancellationToken);

         result.Should().Be("1 item in Groceries");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }

   [Fact]
   public void GetWithPlaceholders_LocalFallbackPlural_RendersInItsForm()
   {
      using var client = new TranslationToolsClient(
         new HttpClient(new NotFoundHandler()),
         Options.Create(new TranslationToolsClientOptions { ApiKey = "api-key" }),
         new LocalTranslationToolsClientCache()
      );

      Translations.SetClient(client);
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var translation = new TranslationRef("Fixture.App:/Localizations.resx", "Cart.ItemCount");
      var localeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["pl"] = CartPl };

      var result = Translations.GetWithPlaceholders(
         translation,
         new CultureInfo("pl"),
         defaultValue: null,
         localeValues,
         bindings: new Dictionary<string, string?> { ["cartName"] = "Groceries" },
         knownSet: null,
         pluralNumber: 22);

      result.Should().Be("22 produkty w Groceries");
   }

   private string Render(string locale, long number, params (string Locale, string Value)[] values)
   {
      Translations.SetClient(new LocaleClient(values.ToDictionary(v => v.Locale, v => v.Value)));
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, _logger);

      return Translations.WithPlaceholders(_cart)
         .ForLocale(new CultureInfo(locale))
         .SetPlaceholder("cartName", "Groceries")
         .SetPluralNumber(number)
         .Render();
   }

   private sealed class NotFoundHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
      }
   }

   private sealed class LocaleClient : ITranslationToolsClient
   {
      private readonly IReadOnlyDictionary<string, string> _valuesByLocale;

      public LocaleClient(IReadOnlyDictionary<string, string> valuesByLocale) => _valuesByLocale = new Dictionary<string, string>(valuesByLocale, StringComparer.OrdinalIgnoreCase);

      public Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default)
         => GetAsync(translation, CultureInfo.CurrentUICulture, cancellationToken);

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
      {
         _valuesByLocale.TryGetValue(locale.Name, out var value);
         return Task.FromResult(new TranslationItemResponse { Origin = translation.Origin, Key = translation.Key, Value = value ?? _valuesByLocale.GetValueOrDefault("") });
      }

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
         => GetAsync(translation, locale, cancellationToken);

      public Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
         => Task.FromResult(new TranslationLocaleSnapshot(locale.Name, new Dictionary<TranslationRef, string?>()));
   }
}
