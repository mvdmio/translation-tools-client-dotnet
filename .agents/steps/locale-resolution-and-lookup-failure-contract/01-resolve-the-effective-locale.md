# 01 — Resolve the effective locale for every lookup

Status: done

## What to build

A translation lookup made under the invariant culture reaches the service instead of collapsing the
request path. Before a lookup consults the cache or builds a request, the client turns the caller's
locale into the **effective locale**: a locale whose name is blank is replaced by the configured
`DefaultLocale`. Nothing else changes. A locale the caller names is used as named — no walk up the
culture parent chain, so `nl-BE` stays `nl-BE` and misses if the service does not hold it.

Every path that reads a locale name goes through resolution: the single-key lookup, the whole-locale
lookup, the refresh performed during initialization, and the cache-inspection and invalidation
helpers. Because resolution runs before the cache is consulted, the cache is keyed by the effective
locale, so the empty string stops being a cache bucket and an invariant-culture lookup shares one
entry with a lookup made under the default locale.

A fallback that is itself broken must fail at startup, so `DefaultLocale` is validated when the
client is constructed, next to the existing `ApiKey` check: it throws when blank or when the runtime
does not recognise it as a locale.

Preloading is deliberately left alone. Resolution decides what one lookup runs against; preloading
decides which locales are worth fetching up front. The existing special case that drops the
invariant culture from the set of locales to preload stays exactly as it is.

## Acceptance criteria

- [x] A single-key lookup made while the current UI culture is the invariant culture requests the
      path segment for `DefaultLocale`, asserted on the URL a stub `HttpMessageHandler` received.
      This is the regression test for the reported bug.
- [x] A lookup under the invariant culture and a lookup under the default locale share one cache
      entry: the second makes no request.
- [x] A lookup for a named locale is requested as named, and a miss produces no follow-up request for
      a parent locale.
- [x] The whole-locale lookup, the refresh performed during initialization, and the cache-inspection
      and invalidation helpers all run against the effective locale.
- [x] Constructing a client with a blank `DefaultLocale`, or one the runtime does not recognise,
      throws; a test covers this alongside a test for the existing `ApiKey` check.
- [x] Preloading is unchanged: the invariant culture is still dropped from the locales fetched at
      startup, and an application running under the invariant culture with no supported locales
      configured still makes no request during initialization.
- [x] The existing integration host gains a case where a job-like lookup runs under the invariant
      culture end to end, through a generated accessor, and returns the served value.
- [x] The README options table's `DefaultLocale` row describes the substitution the client now
      performs, rather than a promise nothing reads.
- [x] The full test suite is green.

## Outcome

Implemented `TranslationToolsClient.ResolveEffectiveLocale(CultureInfo)`, a private helper that
returns `Options.DefaultLocale` when `locale.Name` is blank/whitespace, otherwise `locale.Name`
unchanged. Wired it into every locale-name read the step called out: `GetInternalAsync`,
`GetLocaleAsync`, `RefreshLocaleAsync`, `TryGetCached`, `InvalidateLocale`, and `Invalidate`.
`ApplyLocaleUpdateAsync`/`ApplyUpdateAsync` (live-update paths driven by locales the service
already named) and `GetSupportedLocales` (preloading) were left untouched, per the step file.

Added constructor validation for `Options.DefaultLocale` right next to the existing `ApiKey`
check: throws `ArgumentException` when blank, and when `new CultureInfo(value)` throws
`CultureNotFoundException` for a value the runtime doesn't recognise.

Rewrote the `DefaultLocale` row in `src/mvdmio.TranslationTools.Client/Readme.md`'s options table
to describe the substitution that now runs on every lookup, instead of the old "used when no
specific locale can be resolved" wording.

Tests added:
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/LocaleResolutionTests.cs` — the
  three locale-resolution behaviors from the spec's testing decisions (invariant-culture lookup
  hits `DefaultLocale`'s path; invariant-culture and default-locale lookups share one cache entry;
  a named locale like `nl-BE` is requested as named with no parent-chain follow-up).
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/TranslationToolsClientConstructionTests.cs`
  — new file, since no constructor-validation tests existed yet for either option. Covers a blank
  `ApiKey` (the existing, previously untested check) alongside a blank and an unrecognised
  `DefaultLocale`.
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/StartupAndLiveUpdateIntegrationTests.cs`
  — new case `Lookup_UnderInvariantCulture_ShouldResolveToDefaultLocale_AndReturnServedValue`: an
  application running entirely under the invariant culture (no locales preloaded, matching the
  reported job scenario) reads `Localizations.GetAsync("Button.Save")` and gets back the value the
  in-process host served for `DefaultLocale`.

Deviation: the integration test host
(`test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/_Fixture/TranslationToolsIntegrationTestHost.cs`)
only had a route for whole-locale fetches, not for a single translation key. Added a
`/api/v1/translations/{origin}/{locale}/{key}` route so the new integration test could exercise
the single-key path (the one the reported bug actually broke). Discovered along the way that
ASP.NET Core route binding leaves an escaped `/` (`%2F`) within a path segment undecoded even
though it decodes other escapes like `%3A`; the new route handler calls
`Uri.UnescapeDataString(origin)` to fully recover the original `<project>:/path.resx` origin
before constructing a `TranslationRef`.

Full solution build and `dotnet test` are green: 91 unit tests, 7 integration tests, 19 tool unit
tests — no failures, no regressions in pre-existing tests.
