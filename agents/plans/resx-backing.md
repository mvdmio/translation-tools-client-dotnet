# Resx Backing Plan

## Goal

- Restore `.resx` as the primary authoring format.
- Remove the manual manifest-class workflow from the default path.
- Replace `.mvdmio-translations.snapshot.json` with `.resx`-backed local fallback data.
- Keep API-driven runtime translations, cache hydration, and live updates.
- Keep explicit `pull`/`push` tooling as the main sync workflow. Remove `migrate` from the main story.
- Use Roslyn source generation to emit designer-shaped resource classes from `.resx` without checked-in `*.Designer.cs` files.
- Suppress built-in `.resx` designer generation for all project `.resx` files when the package is installed.

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
- `translations pull` should be non-destructive by default: add/update local files and entries, but do not delete local content unless explicitly requested
- `translations pull --prune` can opt into deleting local files and entries that are no longer present remotely
- Startup sync should be optional, not required.
- If startup sync exists, it should be additive only and disabled by default.
- This keeps production startup read-mostly and avoids every app instance mutating server state automatically.

### DX Model

- Developers add/edit translations in `.resx` files.
- Strongly typed accessors are generated from `.resx`, not hand-authored in manifest classes.
- Developers should consume the same resource-set-shaped generated types they would naturally expect from `.resx`.
- Example:
  - `Errors.resx` -> `Errors.Title`
  - `Admin/Labels.resx` -> `Admin.Labels.Title`
- The generated API should still expose:
  - string properties
  - key constants
  - `Get(...)`
  - `GetAsync(...)`
- Project `.resx` files should have one strongly typed surface, not parallel TranslationTools and built-in designer classes.
- New keys should appear through design-time source generation shortly after save; no checked-in `*.Designer.cs` file is required.

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
- do not overwrite existing remote values, including default-locale values

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
- response scope should be request-driven; do not return every project locale by default

Semantics:

- additive merge only
- no deletes
- no authoritative overwrite, including for default-locale values
- safe to call at every startup

Client changes:

- default `Initialize(...)` path remains fetch-and-hydrate
- if startup sync mode is enabled and a local manifest exists, `Initialize(...)` becomes sync-and-hydrate
- remove the old manifest/snapshot fallback path as part of the `.resx` cutover

### 4. Generate Designer-Shaped Accessors From `.resx`

Recommended end-state:

- remove the need for hand-authored `[Translations]` manifest classes
- source generator reads `.resx` files via `AdditionalFiles`
- generator emits the accessor classes directly
- generator emits one type per resource set using the expected `.resx` class names and namespaces
- managed root is always the project root; do not introduce a separate configurable source root for generated types
- generated namespace = project root namespace + relative folder path from the project root
- generated type name = base `.resx` file name without locale suffix or extension
- examples:
  - `Errors.resx` -> `<RootNamespace>.Errors.Title`
  - `Admin/Labels.resx` -> `<RootNamespace>.Admin.Labels.Title`
  - `Resources/Errors.resx` -> `<RootNamespace>.Resources.Errors.Title`
- generator still emits:
  - string properties
  - key constants
  - `Get(...)`
  - `GetAsync(...)`
- generation stays design-time and build-time only; do not generate checked-in `*.Designer.cs` files

Packaging changes:

- extend the existing `buildTransitive` props/targets so project `.resx` files are also visible to the source generator
- package targets should take ownership of all project `.resx` files and suppress built-in designer generation for all of them by default
- package targets should also stamp those `.resx` items with TranslationTools-specific metadata so downstream build logic and diagnostics can treat them as package-managed resources
- expose enough metadata for stable generation:
  - project root namespace
  - relative path
  - logical resource name
  - locale
  - resource-set identity

### 5. Own The `.resx` Designer Surface

Recommendation:

- TranslationTools-generated resource classes become the primary strongly typed API for all project `.resx` files once the package is installed
- suppress built-in `ResXFileCodeGenerator` / `PublicResXFileCodeGenerator` output for all project `.resx` files
- do not build a custom Visual Studio `<Generator>` implementation; rely on Roslyn source generation so IDE, CLI, and CI stay in sync
- do not support mixed mode inside a project; the package should own the full `.resx` surface

This means developers edit `Errors.resx` and consume `Errors.Title`, rather than translating between `.resx` resource names and a separate aggregate class.

Important:

- suppressing built-in designer generation does not remove the IDE `.resx` editing experience
- it only removes the extra `*.Designer.cs` wrapper
- add diagnostics for stale `*.Designer.cs` files or project `.resx` files that still opt into the built-in generator
- source generators already rerun during design-time compilation after save, which should be fast enough for the intended DX
- avoiding a custom IDE-only generator keeps behavior consistent across Visual Studio, Rider, CLI, and CI
- package installation should be enough to flip the project into TranslationTools-managed `.resx` mode without requiring per-file opt-in

So the preferred order is:

1. package installation takes ownership of all project `.resx` files
2. suppress the built-in `.resx` designer output for those files
3. let the TranslationTools source generator own the strongly typed resource classes

### 6. Define A Stable Key Mapping Rule

This needs to be locked down early. The current migrate logic has a one-time convenience rule where a single resource set can keep raw keys, but that becomes unstable once a second resource set appears.

Recommended rule:

- resource-set identity should be the normalized relative file path from the project root, without locale suffix or `.resx` extension
- API key should always be derived from resource-set identity plus local entry key
- example: `Errors.Title`, `Admin.Labels.Title`
- normalize path separators to `.` for API key generation and reverse mapping
- generate the strongly typed API from the same normalized path model used for key generation
- preserve the original `.resx` entry key text in the API key; do not normalize entry keys to generated property names

Why:

- stable over time
- reversible for `pull`
- collision-resistant
- works naturally with multiple `.resx` files

Important:

- path normalization can still create collisions, for example `Shared.Validation.resx` and `Shared/Validation.resx`
- those collisions should be treated as hard errors with explicit diagnostics

Generated member naming:

- generated property names should be deterministic and culture-invariant
- generated property names should be derived from local `.resx` entry names using a stable identifier-normalization rule
- identifier normalization should only affect generated member names, not API key identity
- generated property names should be PascalCase
- separator characters in local keys should collapse into word boundaries, not be preserved literally
- namespace separators inside local keys should normalize to `_` in generated property names
- C# keywords should normalize to regular PascalCase identifiers rather than escaped identifiers
- examples:
  - local key `save.button` -> generated property `Save_Button`
  - local key `save-button` -> generated property `SaveButton`
  - local key `class` -> generated property `Class`
  - local key `123Title` -> generated property `_123Title`

Collision policy:

- file-path collisions after normalization are hard errors in source generation, `push`, and `pull`
- property-name collisions after identifier normalization are hard errors in source generation, `push`, and `pull`
- diagnostics should point to both conflicting files or keys when possible
- do not silently pick a winner or auto-rename one side

If we want a flatter public API later, that should be a generated-code concern, not an API-key identity concern.

### 7. Rework CLI Around `.resx`

`translations migrate`

- remove from the main flow
- remove the command rather than keeping a transition alias

`translations push`

- scan `.resx`, not manifest classes
- keep this as the explicit authoritative sync command
- current delete/update semantics can stay here because this is the intentional maintenance command

`translations pull`

- write `.resx` files, not manifest `.cs` plus snapshot JSON
- update the default/base file and locale-specific files using the stable key mapping
- default behavior: add missing files and entries, update existing values, do not delete local entries or locale files that are absent remotely
- `--prune` behavior: remove local entries and locale files that are absent remotely
- treat the base file and locale-specific files consistently: deletions only happen under `--prune`

This gives a clean split:

- push: authoritative, explicit
- pull: download/update local `.resx` safely by default, with optional prune semantics
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
- generated designer-shaped resource classes, accessors, and keys from `.resx`
- suppression of built-in designer generation for all project `.resx` files
- docs/examples shifted to `.resx`-first usage

Why second:

- restores the main DX benefit
- can ship once the local resource model is already proven

### Phase 3. CLI Rewrite + Explicit Sync Workflow

Deliver:

- `pull` writes `.resx`
- `push` scans `.resx`
- `migrate` removed
- snapshot writer/parser removed
- manifest file builder/parser removed

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
- remove manifest-based generation support

## Testing Plan

### Client

- unit tests for resource-manifest loading
- unit tests for fallback order changes
- unit tests for optional startup sync request/response handling
- unit tests for cache hydration from sync response

### Source Generator

- generator tests for `.resx` discovery
- generator tests for resource-set class/property/key emission
- generator tests for duplicate key handling
- generator tests for locale/resource-set mapping
- generator tests for diagnostics when project `.resx` files still have built-in designer metadata or stale `*.Designer.cs` files

### Tooling

- tests for `.resx` scan to API payload conversion
- tests for `pull` writing/updating `.resx`
- tests for `pull` default non-destructive behavior
- tests for `pull --prune` deleting removed entries and empty locale files
- tests for stable reverse mapping from API key -> resource set -> local key
- tests for absence/removal of `migrate` command

### Integration

- end-to-end test: offline/no HTTP -> generated property resolves local `.resx` fallback
- end-to-end test: explicit `push`/`pull` workflow updates remote/local state correctly
- end-to-end test: optional startup sync -> cache hydrated -> generated property resolves translated value

## Recommendation

Recommended product stance:

1. use `.resx` as the source of truth
2. generate designer-shaped TranslationTools APIs directly from `.resx` so developers edit `Errors.resx` and consume `Errors.Title`
3. suppress built-in `.resx` designer generation for all project `.resx` files when the package is installed
4. keep `push`/`pull` as the primary sync workflow
5. make startup sync optional and additive-only, not required
6. implement the generated surface with Roslyn source generation, not a custom Visual Studio generator
7. remove manifest/snapshot workflows instead of keeping a compatibility transition path

Implementation order:

1. shared `.resx` model plus local runtime fallback
2. resx-driven code generation that emits designer-shaped resource classes and suppresses built-in designer output for managed `.resx`
3. explicit `.resx`-based `push` and `pull`
4. optional startup sync only if we still need the convenience after the tooling flow is in place

That keeps the design predictable, preserves the `.resx` IDE workflow, aligns authoring with consumption, avoids checked-in designer files, and avoids making runtime mutation of remote translation state part of the default app startup path.
