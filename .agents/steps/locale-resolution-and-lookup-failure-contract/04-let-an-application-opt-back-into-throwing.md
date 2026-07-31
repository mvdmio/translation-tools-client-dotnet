# 04 — Let an application opt back into throwing

Status: done

## What to build

An application that wants lookup failures to be loud can have them. `ThrowOnLookupError`, default
`false`, makes a single-key lookup rethrow instead of answering with the local fallback. It is named
and shaped after the existing `ThrowOnPlaceholderError`, so the package has one way of expressing
"throw instead of degrade" and a developer who knows one knows the other.

Turning it on changes only whether the failure is rethrown. Classification still exists to decide the
log level, and a cancellation the caller requested is still a cancellation rather than a lookup
error.

## Acceptance criteria

- [ ] The client options gain `ThrowOnLookupError`, default `false`, documented in the same shape as
      `ThrowOnPlaceholderError`.
- [ ] With the option set, each failure class — `401`, `404`, `500`, a transport exception, an
      undeserialisable body — throws from a single-key lookup.
- [ ] With the option unset, every behaviour from the previous step is unchanged.
- [ ] A caller's cancellation still propagates as a cancellation whether or not the option is set.
- [ ] The README options table gains a `ThrowOnLookupError` row.
- [ ] The full test suite is green.

## Outcome

Added `TranslationToolsClientOptions.ThrowOnLookupError` (`bool`, default `false`), documented in
the same XML-doc shape as `ThrowOnPlaceholderError`. Added `TranslationLookupException` (a sealed
`Exception` with `message`/`innerException` constructor, at
`src/mvdmio.TranslationTools.Client/TranslationLookupException.cs`), mirroring
`PlaceholderSubstitutionException`'s shape.

`TranslationToolsClient.FetchTranslationOrFallbackAsync`'s four degrade points (transport
exception, unsuccessful status code, undeserialisable body via a throw, undeserialisable body via a
`null` result) now all route through one new helper, `HandleDegradedLookup`. It still runs the same
classification (which decides the log level and the "could not answer" vs "no value for this key"
wording) at every call site; the only change is what it does with that classification. With
`ThrowOnLookupError` set it throws `TranslationLookupException` (message built from the
classification, wrapping the original exception when there is one) instead of logging and building
the local fallback. With the option unset — the existing default — behavior from step 03 is
untouched: log at the classified level and return the fallback tuple. This follows the same shape
as `PlaceholderSubstitution.Degrade`/`WarnOrThrow`, which likewise throw instead of warning rather
than doing both.

A caller's own cancellation is unaffected: both `SendAsync` and the deserialization step still
`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) => throw;`
*before* reaching `HandleDegradedLookup`, so classification and `ThrowOnLookupError` never see it —
it propagates as `OperationCanceledException` regardless of the option, per the acceptance
criteria.

`GetLocaleAsync`/`FetchLocaleAsync` (whole-locale) were untouched; they already throw
unconditionally per step 03 and are unaffected by this option.

Added the `ThrowOnLookupError` row to the options table in
`src/mvdmio.TranslationTools.Client/Readme.md`. The "Local fallback" section itself (its full
rewrite describing the failure contract) is explicitly step 07's job per the spec's Package and
documentation section, so it was left alone here.

Tests added in
`test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/ThrowOnLookupErrorTests.cs` (new
file, following `LookupFailureFallbackTests`'s pattern with its own private `StatusHandler`/
`ThrowingHandler`/`BadJsonHandler`/`NeverCompletingHandler`): each failure class (`401`, `404`,
`500`, a transport exception, an undeserialisable body) throws `TranslationLookupException` when
`ThrowOnLookupError` is set; a caller's cancellation still throws `OperationCanceledException`
rather than `TranslationLookupException` even with the option set; and one parametrised case
confirms the option unset still degrades (regression guard for step 03's behavior, alongside the
already-passing pre-existing `LookupFailureFallbackTests` suite which construct clients with the
option left at its default).

Full solution build and `dotnet test` are green: 122 unit tests (113 + 9 new), 7 integration tests,
19 tool unit tests — no failures, no regressions.

No deviations from the step or spec.
