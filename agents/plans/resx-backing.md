# Resx Backing Plan

## Goal

- Restore `.resx` as the primary authoring format.
- Remove the manual manifest-class workflow from the default path.
- Replace `.mvdmio-translations.snapshot.json` with `.resx`-backed local fallback data.
- Keep API-driven runtime translations, cache hydration, and live updates.
- Keep explicit `pull`/`push` tooling as the main sync workflow. Remove `migrate` from the main story.
- Avoid compile-time conflicts with any built-in `.resx` designer classes.

## Current State

- Keys/default values are authored in `[Translations]` manifest classes.
- `translations pull` writes two artifacts:
  - generated manifest `.cs`
  - `.mvdmio-translations.snapshot.json`
- Runtime lookup order today:
  - sync: cache -> embedded snapshot -> manifest `DefaultValue` -> key
  - async: embedded snapshot -> HTTP -> manifest `DefaultValue` -> key
- `translations push` scans manifest classes.
- `translations migrate` is the only place that already understands `.resx` as a project-wide source format.

## Target Model

### Source of Truth

- `.resx` files become the canonical local translation source.
- Base `.resx` file represents the app's shipped default-locale values.
- Localized `.resx` files represent shipped locale fallbacks.
- The client runtime and tooling derive their manifest/key set from `.resx`, not from generated C# manifests or snapshot JSON.

### Runtime Model

- On startup, the client loads local translation metadata and fallback values from compiled `.resx` resources.
- The client can still hydrate its cache from the API for the locales the app wants in memory.
- The client hydrates its cache from that response.
- Local `.resx` data remains the offline fallback when the cache is empty.

### Sync Model

- Primary sync path should be explicit tooling:
  - `translations push` publishes local `.resx` keys/defaults to the API
  - `translations pull` updates local `.resx` files from API state
- Startup sync should be optional, not required.
- If startup sync exists, it should be additive only and disabled by default.
- This keeps production startup read-mostly and avoids every app instance mutating server state automatically.

### DX Model

- Developers add/edit translations in `.resx` files.
- Strongly typed accessors are generated from `.resx`, not hand-authored in manifest classes.
- TranslationTools-generated accessors should not share type names with `.resx` designer-generated classes.
- The generated API should still expose:
  - string properties
  - key constants
  - `Get(...)`
  - `GetAsync(...)`

## Recommended Design

### 1. Introduce A Shared Resx Manifest Model

Create one internal model used by both runtime and CLI:

- `TranslationResourceManifest`
- `TranslationResourceSet`
- `TranslationResourceItem`

Each item should carry at least:

- API key
- resource-set identity
- local `.resx` key
- default-locale value
- optional shipped localized values

Implementation note:

- Reuse the existing `ResxMigrationScanner`, `ResxResourceSetParser`, and `ProjectTranslationStateBuilder` logic as the starting point.
- Move the reusable parts out of `mvdmio.TranslationTools.Tool.Migrate` into a shared location so the client package can use the same rules as the CLI.

### 2. Replace Snapshot Fallback With Resx Fallback

Replace `TranslationManifestRuntime` snapshot loading with resource-manifest loading.

New fallback order:

- sync: runtime cache -> local `.resx` resource -> default value -> key
- async: runtime cache -> local `.resx` resource -> HTTP -> default value -> key

Notes:

- This keeps offline behavior without embedding a custom JSON snapshot.
- `GetAsync(...)` should check local `.resx` before going to HTTP.
- Live-update cache APIs can stay as they are.

### 3. Prefer Explicit Tooling Sync, Keep Startup Sync Optional

Recommended default:

- do not require runtime write-back during app startup
- use `translations push` as the authoritative publish step
- use `translations pull` to refresh local `.resx` from remote state when needed

Why:

- easier to reason about
- avoids hidden writes from every running instance
- avoids polluting remote projects from local/dev environments
- keeps app startup simpler and more deterministic

Optional startup sync mode can still exist for teams that want it.

Suggested option shape:

- `TranslationToolsClientOptions.StartupSyncMode`
- modes:
  - `Off` (recommended default)
  - `AddMissingKeys`

If enabled, startup sync should use additive-only semantics:

- create missing keys
- create missing locale values
- do not remove keys
- do not overwrite existing remote values

Suggested shape:

- `POST /api/v1/translations/project/sync`

Suggested request:

- local manifest built from `.resx`
- resolved default locale
- shipped locales discovered from `.resx`
- locales requested for cache hydration

Suggested response:

- default locale
- returned locales
- full translation dictionary for requested locales

Semantics:

- additive merge only
- no deletes
- no authoritative overwrite
- safe to call at every startup

Client changes:

- default `Initialize(...)` path remains fetch-and-hydrate
- if startup sync mode is enabled and a local manifest exists, `Initialize(...)` becomes sync-and-hydrate
- if no local manifest exists, keep the current fetch behavior as a compatibility fallback

### 4. Generate Strongly Typed Accessors From `.resx`

Recommended end-state:

- remove the need for hand-authored `[Translations]` manifest classes
- source generator reads `.resx` files via `AdditionalFiles`
- generator emits the accessor classes directly
- generator emits one aggregate configured container type, not one generated type per resource set

Packaging changes:

- extend the existing `buildTransitive` props/targets so project `.resx` files are also visible to the source generator
- expose enough metadata for stable generation:
  - project root namespace
  - relative path
  - logical resource name
  - locale

Compatibility plan:

- keep current manifest-based generation working for one transition phase
- add resx-based generation beside it
- mark manifest-first docs/tooling as deprecated once resx flow is stable

### 5. Avoid `.resx` Designer Conflicts

Recommendation:

- do not rely on built-in `.resx` strongly typed designer classes as the main TranslationTools API
- generate a separate aggregate class such as `Localizations`
- keep the generated type name configurable so teams can avoid collisions with their own resource names

This means standard `.resx` designer classes can continue to exist without compile-time conflict, because they live as separate types.

If a team does not want duplicate generated surfaces, provide an opt-in way to suppress designer generation for TranslationTools-managed `.resx` files.

Important:

- disabling designer generation does not remove the IDE `.resx` editing experience
- it only removes the extra `*.Designer.cs` wrapper

So the preferred order is:

1. avoid collisions by generator design
2. offer designer suppression as an optional cleanup step

### 6. Define A Stable Key Mapping Rule

This needs to be locked down early. The current migrate logic has a one-time convenience rule where a single resource set can keep raw keys, but that becomes unstable once a second resource set appears.

Recommended rule:

- API key should always be derived from resource-set identity plus local entry key
- example: `Errors.Title`, `Admin.Labels.Title`

Why:

- stable over time
- reversible for `pull`
- collision-resistant
- works naturally with multiple `.resx` files

If we want a flatter public API later, that should be a generated-code concern, not an API-key identity concern.

### 7. Rework CLI Around `.resx`

`translations migrate`

- deprecate
- keep temporarily as a no-op wrapper or alias if needed for transition messaging

`translations push`

- scan `.resx`, not manifest classes
- keep this as the explicit authoritative sync command
- current delete/update semantics can stay here because this is the intentional maintenance command

`translations pull`

- write `.resx` files, not manifest `.cs` plus snapshot JSON
- update the default/base file and locale-specific files using the stable key mapping
- preserve empty locale files only if needed for locale intent

This gives a clean split:

- push: authoritative, explicit
- pull: download/update local `.resx`
- startup sync: optional additive convenience mode

## Phased Delivery

### Phase 1. Shared Resx Manifest + Local Fallback

Deliver:

- shared resx scanning/parsing model
- runtime fallback to local `.resx`
- no source-generator UX change yet

Why first:

- solves the local data model first
- reuses most of the existing client runtime shape
- lowers risk before changing code generation

### Phase 2. Resx-Driven Source Generation

Deliver:

- `.resx` discovery through `AdditionalFiles`
- generated accessors/keys from `.resx`
- compatibility support for existing `[Translations]` classes
- docs/examples shifted to `.resx`-first usage

Why second:

- restores the main DX benefit
- can ship once the local resource model is already proven

### Phase 3. CLI Rewrite + Explicit Sync Workflow

Deliver:

- `pull` writes `.resx`
- `push` scans `.resx`
- `migrate` deprecated
- snapshot writer/parser removed
- manifest file builder/parser removed from the default path

### Phase 4. Optional Startup Sync

Deliver:

- new API sync contract
- optional additive startup sync mode
- cache hydration from sync response

### Phase 5. Cleanup

Deliver:

- remove snapshot embedding from `buildTransitive`
- remove `TranslationSnapshotFile*` code
- remove manifest-specific docs from README/package docs
- decide whether manifest-based generation stays as a compatibility mode or is removed in the next breaking release

## Testing Plan

### Client

- unit tests for resource-manifest loading
- unit tests for fallback order changes
- unit tests for no-manifest compatibility fallback
- unit tests for optional startup sync request/response handling
- unit tests for cache hydration from sync response

### Source Generator

- generator tests for `.resx` discovery
- generator tests for class/property/key emission
- generator tests for duplicate key handling
- generator tests for locale/resource-set mapping
- generator tests for coexistence with old manifest mode

### Tooling

- tests for `.resx` scan to API payload conversion
- tests for `pull` writing/updating `.resx`
- tests for stable reverse mapping from API key -> resource set -> local key
- tests for `migrate` deprecation behavior

### Integration

- end-to-end test: offline/no HTTP -> generated property resolves local `.resx` fallback
- end-to-end test: explicit `push`/`pull` workflow updates remote/local state correctly
- end-to-end test: optional startup sync -> cache hydrated -> generated property resolves translated value

## Open Questions

1. Public generated shape

- one class per resource set is simplest and closest to `.resx`
- one flat `Localizations` class is closer to the current library API
- recommend deciding this before Phase 2

2. Designer suppression default

- we should not globally disable `.resx` designer generation for all consumers
- recommend opt-in suppression only for TranslationTools-managed `.resx` files

3. Sync response scope

- all project locales could be large
- supported/requested locales are probably enough for hydration
- recommend request-driven hydration scope

4. Default value overwrite policy

- startup sync should stay additive only
- explicit `push` can remain authoritative
- this split seems clean and should be documented clearly

5. Removal timing

- keep manifest/snapshot support for one transition release
- remove only after `.resx` generation and `.resx` pull/push are stable

## Recommendation

Recommended product stance:

1. use `.resx` as the source of truth
2. generate a separate aggregate TranslationTools API from `.resx`
3. keep `push`/`pull` as the primary sync workflow
4. make startup sync optional and additive-only, not required
5. do not globally disable `.resx` designer generation, but offer opt-in suppression for teams that want a single generated surface

Implementation order:

1. shared `.resx` model plus local runtime fallback
2. resx-driven code generation with non-conflicting type names
3. explicit `.resx`-based `push` and `pull`
4. optional startup sync only if we still need the convenience after the tooling flow is in place

That keeps the design predictable, preserves the `.resx` IDE workflow, and avoids making runtime mutation of remote translation state part of the default app startup path.
