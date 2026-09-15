# 01 — Split oversized client and generator types

Status: pending
Blocked by: none

## What to build

The source generator and the main client type are already at the project's size limit. Later steps add a runtime catalog of every translation key and a missing-key send inside Initialize. Split those two types first so those additions fit without another extract. Initialize, generated accessors, and every public API stay as they are.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.SourceGenerator, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, SourceGeneratorEndToEnd, mvdmio.TranslationTools.Tool, mvdmio.TranslationTools.Tool.Tests.Unit

- `src/mvdmio.TranslationTools.Client.SourceGenerator/TranslationManifestGenerator.cs` — `TranslationManifestGenerator` (already past ~500 LOC)
- `src/mvdmio.TranslationTools.Client/TranslationToolsClient.cs` — `TranslationToolsClient` (already near ~500 LOC)
- New helper files beside those two as needed to hold existing logic

## Acceptance criteria

- [ ] `TranslationManifestGenerator.cs` and `TranslationToolsClient.cs` are each well under 500 LOC
- [ ] Initialize still only preloads supported locales and starts the heartbeat
- [ ] Generated accessors and origins are unchanged
- [ ] Solution builds and the existing test suite is green
