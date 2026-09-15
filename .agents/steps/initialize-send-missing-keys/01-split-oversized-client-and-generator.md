# 01 — Split oversized client and generator types

Status: done
Blocked by: none

## What to build

The source generator and the main client type are already at the project's size limit. Later steps add a runtime catalog of every translation key and a missing-key send inside Initialize. Split those two types first so those additions fit without another extract. Initialize, generated accessors, and every public API stay as they are.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.SourceGenerator, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, SourceGeneratorEndToEnd, mvdmio.TranslationTools.Tool, mvdmio.TranslationTools.Tool.Tests.Unit

- `src/mvdmio.TranslationTools.Client.SourceGenerator/TranslationManifestGenerator.cs` — `TranslationManifestGenerator` (already past ~500 LOC)
- `src/mvdmio.TranslationTools.Client/TranslationToolsClient.cs` — `TranslationToolsClient` (already near ~500 LOC)
- New helper files beside those two as needed to hold existing logic

## Acceptance criteria

- [x] `TranslationManifestGenerator.cs` and `TranslationToolsClient.cs` are each well under 500 LOC
- [x] Initialize still only preloads supported locales and starts the heartbeat
- [x] Generated accessors and origins are unchanged
- [x] Solution builds and the existing test suite is green

## Outcome

Split complete with no behavior change. `TranslationManifestGenerator.cs` is now ~50 LOC (pipeline only). Manifest grouping/`BuildManifests`/`BuildManifest` live in `TranslationManifestBuilder.cs` (~276 LOC); path/MSBuild helpers live in `TranslationManifestPaths.cs` (~214 LOC). Step 02 should edit `TranslationManifestBuilder.BuildManifests`/`BuildManifest`, not the generator file. `TranslationToolsClient` is a partial: main file ~309 LOC (ctors, Initialize, public API, cache store), `TranslationToolsClient.Heartbeat.cs` (~80), `TranslationToolsClient.Fetch.cs` (~122). Initialize still only preloads supported locales then starts heartbeat. Verified: solution build green; unit (162), tool unit (21), and integration (7) tests green.