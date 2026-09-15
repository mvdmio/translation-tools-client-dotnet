using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Placeholders;
using System;
using System.Collections.Generic;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class PlaceholderSubstitutionTests
{
   private static readonly IGlobalPlaceholderResolver NoGlobals = new FakeGlobals();

   [Fact]
   public void Substitute_HelloUserName_ReplacesBinding()
   {
      var result = Render("Hello {userName}!", new() { ["userName"] = "Bob" });
      result.Should().Be("Hello Bob!");
   }

   [Fact]
   public void Substitute_RepeatedTokens_ReplaceAllOccurrences()
   {
      var result = Render("{a}+{b}={a}", new() { ["a"] = "1", ["b"] = "2" });
      result.Should().Be("1+2=1");
   }

   [Fact]
   public void Substitute_LiteralBraces_RenderRaw()
   {
      var result = Render("'{'literal'}'", new());
      result.Should().Be("{literal}");
   }

   [Fact]
   public void Substitute_ApostropheInWord_PreservedLiterally()
   {
      var result = Render("It's {n} cats", new() { ["n"] = "3" });
      result.Should().Be("It's 3 cats");
   }

   [Fact]
   public void Substitute_MissingBinding_StringKeyed_DegradesAndWarns()
   {
      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute("Hi {userName}", new Dictionary<string, string?>(), NoGlobals, knownSet: null, throwOnError: false, warnings.Add);

      result.Should().Be("Hi {userName}");
      warnings.Should().ContainSingle();
   }

   [Fact]
   public void Substitute_UnknownBraceLiteral_StringKeyed_NoWarn()
   {
      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute("'{'x'}'", new Dictionary<string, string?>(), NoGlobals, knownSet: null, throwOnError: false, warnings.Add);

      result.Should().Be("{x}");
      warnings.Should().BeEmpty();
   }

   [Fact]
   public void Substitute_UnknownToken_WithKnownSet_IsInertNoWarn()
   {
      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute(
         "Hi {userName}, {legacyWord}",
         new Dictionary<string, string?> { ["userName"] = "Bob" },
         NoGlobals,
         knownSet: new[] { "userName" },
         throwOnError: false,
         warnings.Add);

      result.Should().Be("Hi Bob, {legacyWord}");
      warnings.Should().BeEmpty();
   }

   [Fact]
   public void Substitute_ManagedMissing_WithKnownSet_DegradesAndWarns()
   {
      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute(
         "Hi {userName}",
         new Dictionary<string, string?>(),
         NoGlobals,
         knownSet: new[] { "userName" },
         throwOnError: false,
         warnings.Add);

      result.Should().Be("Hi {userName}");
      warnings.Should().ContainSingle();
   }

   [Fact]
   public void Substitute_ManagedMissing_ThrowMode_Throws()
   {
      Action act = () => PlaceholderSubstitution.Substitute(
         "Hi {userName}",
         new Dictionary<string, string?>(),
         NoGlobals,
         knownSet: new[] { "userName" },
         throwOnError: true,
         warn: null);

      act.Should().Throw<PlaceholderSubstitutionException>();
   }

   [Fact]
   public void Substitute_ExtraBinding_WarnsButSucceeds()
   {
      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute(
         "Hello {userName}",
         new Dictionary<string, string?> { ["userName"] = "Bob", ["unused"] = "x" },
         NoGlobals,
         knownSet: null,
         throwOnError: false,
         warnings.Add);

      result.Should().Be("Hello Bob");
      warnings.Should().ContainSingle(w => w.Contains("unused"));
   }

   [Fact]
   public void Substitute_RegisteredGlobal_ResolvedAmbiently()
   {
      var globals = new FakeGlobals(("companyName", "Acme"));
      var result = PlaceholderSubstitution.Substitute("Welcome to {companyName}", bindings: null, globals, knownSet: new[] { "companyName" }, throwOnError: false, warn: null);

      result.Should().Be("Welcome to Acme");
   }

   [Fact]
   public void Substitute_GlobalResolveFailure_Degrades()
   {
      var globals = new FakeGlobals { FailToResolve = true };
      globals.Register("companyName");

      var warnings = new List<string>();
      var result = PlaceholderSubstitution.Substitute("Welcome to {companyName}", bindings: null, globals, knownSet: new[] { "companyName" }, throwOnError: false, warnings.Add);

      result.Should().Be("Welcome to {companyName}");
      warnings.Should().ContainSingle();
   }

   [Fact]
   public void Substitute_BindingShadowsGlobal()
   {
      var globals = new FakeGlobals(("companyName", "Acme"));
      var result = PlaceholderSubstitution.Substitute(
         "Welcome to {companyName}",
         new Dictionary<string, string?> { ["companyName"] = "Local" },
         globals,
         knownSet: new[] { "companyName" },
         throwOnError: false,
         warn: null);

      result.Should().Be("Welcome to Local");
   }

   private static string Render(string value, Dictionary<string, string?> bindings)
   {
      return PlaceholderSubstitution.Substitute(value, bindings, NoGlobals, knownSet: null, throwOnError: false, warn: null);
   }

   private sealed class FakeGlobals : IGlobalPlaceholderResolver
   {
      private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

      public FakeGlobals(params (string Name, string? Value)[] values)
      {
         foreach (var (name, value) in values)
            _values[name] = value;
      }

      public bool FailToResolve { get; set; }

      public void Register(string name) => _values[name] = null;

      public IReadOnlyCollection<string> RegisteredNames => _values.Keys;

      public bool IsRegistered(string name) => _values.ContainsKey(name);

      public bool TryResolve(string name, out string? value)
      {
         value = null;
         if (FailToResolve || !_values.TryGetValue(name, out var stored) || stored is null)
            return false;

         value = stored;
         return true;
      }
   }
}
