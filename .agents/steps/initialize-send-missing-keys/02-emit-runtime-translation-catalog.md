# 02 — Emit an assembly-level catalog of every translation key

Status: pending
Blocked by: 01

## What to build

The source generator emits a runtime catalog of every translation key in the `.resx` files it already receives. The catalog includes origin, key, Neutral value, and sibling-locale values. Registration is assembly-level: the catalog is complete when the application assembly loads, even if the app never referenced a generated type.

Generated code is compiled into the consuming assembly, not into the generator, so catalog registration is a public API on the client package. Generator-test runtime stubs currently stub only `TranslationRef` and `Translations`; they must stub that catalog API too, or those compilations fail.

Today a group with no unsuffixed file is skipped, and accessors come only from Neutral entries. Keep that accessor surface. The catalog still includes a key that exists only in a locale sibling, and a key from a locale-suffixed file with no unsuffixed pair. Two `.resx` files that share a key name appear as two catalog entries, one per origin. This step does not add accessors for locale-only keys.

The client does not send yet. Tests assert what the catalog contains.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.SourceGenerator, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, SourceGeneratorEndToEnd

- `src/mvdmio.TranslationTools.Client.SourceGenerator/TranslationManifestGenerator.cs` — `BuildManifests`, `BuildManifest`, grouping that today skips a missing Neutral file
- `src/mvdmio.TranslationTools.Client.SourceGenerator/TranslationManifestEmitter.cs` — accessor emission, left as it is
- `src/mvdmio.TranslationTools.Client.SourceGenerator/TranslationManifestModel.cs` — per-file accessor model
- New catalog types on the client package (public registration API the generated source can call)
- New catalog emission beside the existing generator
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/TranslationManifestGeneratorTests.cs` — catalog contents; runtime stub
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/PlaceholderGeneratorTests.cs` — runtime stub
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/SourceGeneratorEndToEndProjectTests.cs` — generated accessors still compile
- `test/TranslationTools/SourceGeneratorEndToEnd/` — Neutral and sibling `.resx` files the end-to-end catalog covers

## Acceptance criteria

- [ ] The catalog includes a key that exists only in a locale sibling, and a key from a locale-suffixed file with no unsuffixed pair
- [ ] Two origins that share a key name both appear
- [ ] Generated accessors for existing keys still compile and still seed on first lookup
- [ ] A process does not have to reference a generated type for that origin's keys to be in the catalog
- [ ] Solution builds and the existing test suite is green
