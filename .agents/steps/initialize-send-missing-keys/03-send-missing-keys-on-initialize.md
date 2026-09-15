# 03 — Send missing keys after a successful locale preload

Status: done
Blocked by: 01, 02

## What to build

When `Initialize` runs, and every supported-locale preload succeeds, the client posts one project push for every missing key in the catalog. A missing key is one whose origin and key did not appear in any locale snapshot just loaded, including a snapshot entry whose value is empty or null. Extra keys on the service are not removed. Keys the snapshots already showed are not posted, even when empty.

Each missing key becomes one item per locale value: Neutral value as the configured default locale, plus each sibling-locale value. Empty values are omitted. When a sibling locale equals the default locale, the sibling value is the item for that locale and the Neutral value is not also sent as that locale. Prune is false or omitted. Environment is on the body when configured, and omitted when unnamed. If the missing set is empty, or the catalog is empty, `Initialize` does not post a project push.

The send uses the same `api/v1/translations/project` contract `PushGlobalsAsync` already posts to. After a successful send, the client merges the sent values into the local cache (the cache's per-key set, not a locale replace) so a later lookup for a sent key is answered from cache without a single-key GET.

Order inside `Initialize`: preload every supported locale, send missing keys, then start the heartbeat as today. Today's `Initialize` starts the heartbeat after the preload try/finally even when a GET throws; keep that. The send runs only after every GET succeeded. A single failed GET skips the send. `Initialize` still throws on that GET. A failed send is caught inside `Initialize`, logged, and does not throw. Heartbeat still starts after a failed send.

Tests can replace the catalog through a constructor or DI seam. Production uses the generated catalog. Existing unit tests construct the client through the internal constructor and call `Initialize` with no catalog; omitting a replacement must mean an empty catalog, so those tests do not post keys if another assembly's generated catalog has loaded. The public constructor and the DI factory use the generated catalog.

Prefer `Initialize` with a replaced catalog for the send rules. First-access seeding on a single-key lookup stays. `GetLocaleAsync` still does not send keys by itself. `translations push` is unchanged.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, SourceGeneratorEndToEnd

- `src/mvdmio.TranslationTools.Client/TranslationToolsClient.cs` — `Initialize`, `PushGlobalsAsync`, public and internal constructors
- `src/mvdmio.TranslationTools.Client/ITranslationToolsClient.cs` — `Initialize` documentation
- `src/mvdmio.TranslationTools.Client/DependencyInjectionExtensions.cs` — `AddTranslationToolsClient` factory
- `src/mvdmio.TranslationTools.Client/Internal/ProjectGlobalsPushRequest.cs` — current project-push body
- `src/mvdmio.TranslationTools.Client/Internal/LocalTranslationToolsClientCache.cs` — `SetAsync` merge, `SetLocaleAsync` replace
- `src/mvdmio.TranslationTools.Client/Internal/ITranslationToolsClientCache.cs` — cache seam
- New send helper beside the client if `Initialize` would grow past the size limit
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/HeartbeatAndEnvironmentTests.cs` — `CreateClient` / `RecordingHandler` prior art for `Initialize` HTTP
- New unit tests for the send rules with a replaced catalog
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/FakeLogger.cs` — failed-send log

## Acceptance criteria

- [x] After a successful preload, every local key that was not in the snapshots is posted, with Neutral value as the default locale and sibling-locale values present
- [x] A key that appeared in any loaded snapshot is not posted, even when its snapshot value is empty
- [x] A failed locale GET produces no project push of keys, and `Initialize` still throws on that GET
- [x] A failed project push does not throw from `Initialize`, is logged, and the heartbeat still starts
- [x] An empty missing set produces no project push from `Initialize`
- [x] An empty catalog produces no project push from `Initialize`
- [x] Environment is on the body when configured, and omitted when unnamed
- [x] After a successful send, a lookup for a sent key is answered from cache without a single-key GET
- [x] A second `Initialize` in the same process sends nothing when the keys are already on the service
- [x] Two origins that share a key name both appear when both are missing
- [x] A sibling file whose locale equals the default locale wins for that locale
- [x] Solution builds and the existing test suite is green

## Outcome

`Initialize` now sends missing catalog keys after every supported-locale preload succeeds, then starts the heartbeat as before. Send logic lives in `TranslationToolsClient.MissingKeys.cs` (~142 LOC); main client ~317 LOC. `ProjectGlobalsPushRequest` was replaced by `ProjectPushRequest` / `ProjectPushItemRequest` (items + optional Environment/Prune/Globals; null Environment and false Prune omitted from JSON). `PushGlobalsAsync` uses the same type with empty items. Catalog seam: internal ctor takes optional `IEnumerable<TranslationCatalogKey>? catalog` — omit/`null` means empty (so existing unit tests do not post keys from another assembly's `ModuleInitializer`); public ctor and `AddTranslationToolsClient` pass `TranslationCatalog.Entries`. Missing = origin+key absent from every preloaded locale snapshot (empty/null snapshot values still count as present). After a successful POST, sent values are merged via cache `SetAsync`. Failed send is logged at Warning and does not throw. README/version/`InitializeTranslationToolsClientAsync` host recording remain Step 04 — integration Initialize may now attempt a missing-key POST against the fake host; failures are swallowed until Step 04 records/accepts that endpoint. Verified: unit 178, integration 7, SourceGeneratorEndToEnd build green.
