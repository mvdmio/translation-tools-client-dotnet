# 04 — Record the project push at startup and ship the docs

Status: done
Blocked by: 01, 02, 03

## What to build

`InitializeTranslationToolsClientAsync` still preloads locales, then the missing-key send from the previous step, then heartbeat, then the globals push, then live updates when enabled. A failed locale preload still skips the send and does not fail application startup. A failed missing-key send is logged and does not fail application startup; heartbeat still starts. Global placeholder names are still posted after initialize, with empty items, and that post does not require the missing-key items. Empty items must not undo the keys just sent.

The integration host already fakes locale GET, single-key GET, and heartbeat. It now records `POST` of the project push (items, prune, Environment, globals) so tests can see which keys were sent and can tell the missing-key post from the later globals post. The host accepts that POST, because the integration project's generated catalog includes keys some existing snapshots omit.

Prefer `InitializeTranslationToolsClientAsync` for the full startup story. Generated accessors for existing keys still compile and still seed on first lookup. `translations push` stays as it is.

The public documentation on `Initialize` and `InitializeTranslationToolsClientAsync` states the missing-key send, not only locale preload. The client package readme states that initialize sends missing keys after a successful locale preload, and that existing keys are not overwritten. Package version goes from 3.5.1 to 3.6.0.

Do not close the parked whole-locale lookup issue. This spec covers initialize only.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.SourceGenerator, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, SourceGeneratorEndToEnd, mvdmio.TranslationTools.Tool, mvdmio.TranslationTools.Tool.Tests.Unit

- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/_Fixture/TranslationToolsIntegrationTestHost.cs` — locale GET, single-key GET, heartbeat; add project-push recording
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/StartupAndLiveUpdateIntegrationTests.cs` — startup wrapper prior art
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/HeartbeatAndEnvironmentIntegrationTests.cs` — heartbeat after initialize; Environment
- New integration tests for the missing-key send through `InitializeTranslationToolsClientAsync`
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/Localizations.resx` — Neutral catalog keys the host can omit or include
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Integration/Resources/Shared/Errors.resx` — second origin
- `src/mvdmio.TranslationTools.Client/DependencyInjectionExtensions.cs` — `InitializeTranslationToolsClientAsync`, `PushGlobalsAsync`
- `src/mvdmio.TranslationTools.Client/ITranslationToolsClient.cs` — `Initialize` documentation
- `src/mvdmio.TranslationTools.Client/Readme.md` — initialize sends missing keys; existing keys are not overwritten
- `Directory.Build.props` — `TranslationToolsVersion` 3.5.1 → 3.6.0

## Acceptance criteria

- [x] After a successful preload through `InitializeTranslationToolsClientAsync`, every local key that was not in the snapshots is posted, with Neutral value as the default locale and sibling-locale values present
- [x] A failed locale GET produces no project push of keys, and application startup still succeeds
- [x] A failed project push does not throw from the startup wrapper, and the heartbeat still starts
- [x] Environment is on the missing-key body when configured, and omitted when unnamed
- [x] Global placeholder names are still posted after initialize, and that post does not require the missing-key items
- [x] Live updates still start from the startup wrapper after `Initialize` when they are enabled
- [x] Generated accessors for existing keys still compile and still seed on first lookup
- [x] The client package readme states that initialize sends missing keys after a successful locale preload, and that existing keys are not overwritten
- [x] Package version is 3.6.0
- [x] Solution builds and the whole test suite is green

## Outcome

Integration host records and accepts `POST /api/v1/translations/project` (`ProjectPushes` / `ProjectPushRawBodies`, plus `LocaleStatusCode` / `ProjectPushStatusCode`). New `InitializeMissingKeysIntegrationTests` cover missing-key send via `InitializeTranslationToolsClientAsync` (Neutral+sibling, failed locale GET, failed push+heartbeat, Environment omit, globals-only push). Added `Localizations.nl.resx` (`Opslaan fallback`) so the generated catalog carries a sibling value. `InitializeTranslationToolsClientAsync` XML docs, client `Readme.md`, and root `README.md` state missing-key send / no overwrite. `TranslationToolsVersion` → 3.6.0. Adjusted invariant-culture startup test to set explicit `SupportedLocales = [en]` and include Errors in the snapshot so Initialize does not treat empty-preload as “all keys missing” and cache Neutral over the API value. Parked whole-locale lookup issue left open. Verified: unit 178, integration 13, tool unit 21, SourceGeneratorEndToEnd build green.
