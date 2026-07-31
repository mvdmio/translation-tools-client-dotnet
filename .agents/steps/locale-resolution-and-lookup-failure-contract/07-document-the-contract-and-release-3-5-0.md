# 07 — Document the contract and release 3.5.0

Status: done

## What to build

A developer reading the README can rely on the documented behaviour. The package README's "Local
fallback" section currently describes a workflow for keeping `.resx` files in source control and says
nothing about what happens at runtime. It is rewritten to describe what the client actually does when
the service cannot answer: the fallback chain from served value to the `.resx` entry for the
effective locale to the neutral value to the translation key, that a lookup never throws, the log
levels a degraded lookup writes at, the five-second bound, the one-minute suppression window, and the
option that restores throwing.

The options table is checked as a whole: `DefaultLocale`, `ThrowOnLookupError`, and `LookupTimeout`
all present and describing behaviour that now exists.

The package version goes to `3.5.0` — new behaviour, no breaking change to the public API. The
version lives in one place, as a build property shared by every project in the solution. Note that
`AGENTS.md`'s version-bump instruction names two project files belonging to an entirely different
package; ignore it and leave `AGENTS.md` alone.

The decision record and the glossary for this work already exist and need no further change.

## Acceptance criteria

- [ ] The README's "Local fallback" section describes what the client does at runtime when the
      service cannot answer, not a source-control workflow.
- [ ] The README documents the failure contract, the one-minute suppression window, and the log
      levels.
- [ ] The options table lists `ThrowOnLookupError` and `LookupTimeout`, and the `DefaultLocale` row
      describes the substitution the client performs.
- [ ] The shared version build property reads `3.5.0`, and no other version location is edited.
- [ ] `AGENTS.md` is unchanged.
- [ ] No changelog-style notes or release callouts are added to the README.
- [ ] The full test suite is green.

## Outcome

Rewrote the "Local fallback" section of `src/mvdmio.TranslationTools.Client/Readme.md`. It
previously described a source-control workflow (keep `.resx` files in source control, use the CLI
tool to pull updates, initialize, use the members) and said nothing about runtime behaviour. It now
describes: that a single-key lookup never throws; that locale resolution substitutes
`DefaultLocale` for a blank (invariant-culture) locale name before the chain runs; the four-step
fallback chain for a generated accessor (served value, `.resx` entry for the effective locale
matched on its exact name, neutral value, translation key); that placeholders still substitute over
fallback text; that a direct `ITranslationToolsClient` caller with no local text gets a `null`
value instead of an exception; the five-second `LookupTimeout` bound; the one-minute suppression
window opened only by a connection failure, a timeout, or a `5xx` (not a `401`/`404`), with no
background probing; that a degraded value is never cached; the `Error`/`Warning` log-level split
and what each log line names; that a whole-locale lookup still throws; the `ThrowOnLookupError`
option with an example; and that a caller's own cancellation always propagates as a cancellation.

Checked the options table before touching it, per the step's instruction to look at what already
exists: steps 01, 04, and 05 had already added/corrected the `DefaultLocale`, `ThrowOnLookupError`,
and `LookupTimeout` rows respectively, so the table needed no changes — it already lists all three
with behaviour matching what was built.

Bumped `Directory.Build.props`'s `TranslationToolsVersion` from `3.4.0` to `3.5.0` — confirmed by
grep it is the only version-bearing build property in the solution (excluding generated `obj/`
files) and that no `.csproj` hardcodes a version. Left `AGENTS.md` untouched; it names
`mvdmio.Database.PgSQL` project files, which belong to a different package in this repository, as
the spec and step file both call out.

Ran `dotnet build` (0 warnings, 0 errors; the packed nupkg confirms `mvdmio.TranslationTools.Client.3.5.0.nupkg`)
and `dotnet test`: 139 unit tests, 7 integration tests, and 19 tool unit tests all pass, no
regressions.

No deviations from the step or spec.
