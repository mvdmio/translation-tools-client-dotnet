# Send missing keys when the client initializes

Status: ready-for-agent

## Problem Statement

When an application starts, Translation Tools does not receive the translation keys that live only in local `.resx` files. Initialize preloads locales from the service. It does not send local keys. A key reaches the service later, on first single-key lookup, or when someone runs `translations push`. Keys that no request looks up never appear on the service. Translators never see them.

## Solution

When `InitializeTranslationToolsClientAsync` runs, and every supported-locale preload succeeds, the client sends every missing key from the application's `.resx` files. A missing key is one that did not appear in any locale snapshot just loaded. For each missing key, the client sends the Neutral value (as the configured default locale) and every sibling-locale value the app shipped. Keys the service already showed in a loaded locale are left alone. Extra keys on the service are not removed. If preload fails, or if the send fails, the client logs and the application still starts. Heartbeat, global placeholder names, live updates, first-access seeding, and `translations push` stay as they are.

## User Stories

1. As a translator, I want every translation key in the application's `.resx` files to appear on the service after the app starts, so that I can translate strings nobody has clicked yet.
2. As a translator, I want the Neutral value on a newly appeared key, so that I can see the text the developer wrote.
3. As a translator, I want sibling-locale `.resx` values on a newly appeared key, so that existing local translations are not blank on first sight.
4. As a translator, I want a key that already exists on the service to keep its current text, so that initialize does not undo work I already did.
5. As a translator, I want empty locale rows on an existing key left alone, so that initialize does not fill them from `.resx` behind my back.
6. As a translator, I want extra keys on the service to stay there, so that initialize does not delete strings this deployment no longer ships.
7. As a translator, I want a key that exists only in a locale sibling `.resx` to appear on the service, so that locale-only strings are not invisible.
8. As a translator, I want a key from a locale-suffixed `.resx` that has no matching unsuffixed file to appear on the service, so that every `.resx` file in the app is covered.
9. As a translator, I want two `.resx` files that share a key name to appear as two translation keys, so that each origin stays distinct.
10. As a developer, I want initialize to send missing keys without me calling `translations push` at deploy, so that a forgotten CLI step does not hide new strings.
11. As a developer, I want `translations push` to keep working as it does today, so that I can still sync the full catalog from the command line.
12. As a developer, I want first-access seeding on a single-key lookup to keep working, so that a key looked up before a successful initialize send still reaches the service.
13. As a developer, I want initialize to still preload supported locales, so that later lookups can answer from cache.
14. As a developer, I want initialize to still start the heartbeat, so that the service still sees this client.
15. As a developer, I want initialize to still push declared global placeholder names, so that globals keep matching the server.
16. As a developer, I want live updates to still start when I enabled them, so that runtime edits still arrive.
17. As a developer, I want a lookup after initialize to use the values just sent, so that the app does not immediately re-fetch those keys.
18. As a developer, I want a second initialize in the same process to send nothing if the keys are already on the service, so that a repeat call is a no-op for the catalog.
19. As a developer, I want a process with no `.resx` keys to send nothing, so that an empty app does not post an empty catalog.
20. As a developer, I want a process whose keys are already on the service to send nothing, so that a fully synced app is quiet at startup.
21. As an operator, I want a failed locale preload to skip the missing-key send, so that a down service cannot overwrite keys we cannot see.
22. As an operator, I want a failed missing-key send to be logged, so that I can tell the catalog did not sync.
23. As an operator, I want a failed missing-key send not to fail application startup, so that a translation outage is not a failed deploy.
24. As an operator, I want a failed locale preload not to fail application startup, so that today's initialize wrapper stays as it is.
25. As an operator, I want the send to name this Environment, so that keys land in the same Environment the client already fetches.
26. As an operator, I want an unnamed Environment to omit the Environment field, so that blank config still means the unnamed Environment.
27. As an operator, I want missing to be judged only from the locales this app just loaded, so that initialize does not need a new list-keys API.
28. As an operator, I accept that a key which exists only in a locale this app does not preload may be sent, so that we can use the snapshots we already have.
29. As a developer, I want one request for the whole missing set, so that startup cost does not grow one-to-one with the catalog.
30. As a developer, I want empty `.resx` values omitted from the send, so that we do not create blank locale rows from empty entries.
31. As a developer, I want the Neutral value sent as the configured default locale, so that unsuffixed `.resx` text matches `translations push`.
32. As a developer, I want a sibling file whose locale equals the default locale to win for that locale, so that we do not send two values for the same origin, locale, and key.
33. As a developer, I want the catalog complete even if the app never referenced a generated type, so that unused `.resx` files still sync at startup.
34. As a developer, I want generated accessors for keys that already have them to stay as they are, so that this change does not reshape call sites.
35. As a developer, I want the client package readme to say that initialize sends missing keys, so that I do not have to learn this from the code.
36. As a tester, I want to replace the catalog in tests, so that I can assert the send without compiling `.resx` files in every case.
37. As a tester, I want the fake Translation Tools host to record the project push, so that integration tests can see which keys were sent.
38. As a maintainer, I want the package version bumped as a minor increment, so that callers can tell this is new behaviour.

## Implementation Decisions

- The send runs inside client `Initialize`, which `InitializeTranslationToolsClientAsync` already calls. Callers of `Initialize` get the same behaviour.
- Order inside `Initialize`: preload every supported locale, send missing keys, then start the heartbeat as today.
- The send runs only after every supported-locale GET succeeded. A single failed GET skips the send. `Initialize` still throws on that GET, and the existing startup wrapper still catches and logs.
- A failed send is caught inside `Initialize`, logged, and does not throw. Heartbeat still starts after a failed send.
- The source generator emits a runtime catalog of every translation key in the `.resx` files it already receives. The catalog includes origin, key, Neutral value, and sibling-locale values.
- Registration is assembly-level, so the catalog is complete when the application assembly loads. The app does not have to reference every generated type.
- The catalog includes keys that appear only in a locale sibling, even when those keys have no generated accessor. This change does not add accessors for those keys.
- A locale-suffixed `.resx` with no matching unsuffixed file still contributes its keys to the catalog.
- Missing means the origin and key did not appear in any locale snapshot just loaded, including a snapshot entry whose value is empty or null.
- The client posts one project push for the missing set. It uses the same contract the CLI uses for items: origin, locale, key, and value. Prune is false or omitted. The body carries Environment when the client has one, and omits it when the Environment is unnamed.
- If the missing set is empty, the client does not post a project push from `Initialize`. The later globals push is unchanged and still posts empty items when globals exist.
- Each missing key becomes one item per locale value. The Neutral value is an item whose locale is the configured default locale. Each sibling-locale value is an item whose locale is that sibling's name. Empty values are omitted. When a sibling locale equals the default locale, the sibling value is the item for that locale and the Neutral value is not also sent as that locale.
- After a successful send, the client merges the sent values into the local cache so a later lookup or locale read does not need an immediate re-fetch for those keys.
- Global placeholder names stay a separate project push after `Initialize`, with empty items, as today. Empty items must not undo the keys just sent.
- First-access seeding on a single-key lookup stays. A later `GetLocaleAsync` still does not send keys by itself.
- `translations push` is unchanged.
- Live updates still start from the startup wrapper after `Initialize` when they are enabled.
- Tests can replace the catalog through a constructor or DI seam on the client. Production uses the generated catalog.
- Split any type that would grow past the project's size limit. The main client type and the source generator are already near that limit.
- Bump the package version as a minor increment.
- Update the public documentation on `Initialize` so it states the missing-key send, not only locale preload.
- Update the client package readme so it states that initialize sends missing keys after a successful locale preload, and that existing keys are not overwritten.

## Testing Decisions

A good test asserts what the service receives and what the application observes. It does not assert how the catalog is stored or how the diff is computed.

Test the client initialize path, the generated catalog, and the startup wrapper. Prefer `InitializeTranslationToolsClientAsync` for the full startup story. Prefer `Initialize` with a replaced catalog for the send rules. Prefer source-generator tests for what the catalog contains.

Prior art: startup and live-update integration tests, heartbeat and environment integration tests, translation manifest generator tests, and CLI push handler tests. The integration host already fakes locale GET, single-key GET, and heartbeat. It must also record `POST` of the project push (items, prune, Environment, globals).

Cover at least:

- After a successful preload, every local key that was not in the snapshots is posted, with Neutral value as the default locale and sibling-locale values present.
- A key that appeared in any loaded snapshot is not posted, even when its snapshot value is empty.
- A failed locale GET produces no project push of keys.
- A failed project push does not throw from the startup wrapper, and the heartbeat still starts.
- An empty missing set produces no project push from `Initialize`.
- An empty catalog produces no project push from `Initialize`.
- Environment is on the body when configured, and omitted when unnamed.
- After a successful send, a lookup for a sent key is answered from cache without a single-key GET.
- The catalog includes a key that exists only in a locale sibling, and a key from a locale-suffixed file with no unsuffixed pair.
- Two origins that share a key name both appear when both are missing.
- Generated accessors for existing keys still compile and still seed on first lookup.
- Global placeholder names are still posted after initialize, and that post does not require the missing-key items.

## Out of Scope

- Changing `translations push`, including prune.
- Overwriting existing non-empty or empty values on keys the snapshots already showed.
- Removing extra keys from the service.
- A new API that lists every key in an Environment.
- Sending missing keys from `GetLocaleAsync` itself when it is called later. A related issue remains for that path.
- Failing application startup when preload or send fails.
- Removing first-access seeding.
- Generating new accessors for locale-only keys.
- Changing Environment name rules, heartbeat, or live updates.
- Combining the missing-key post with the globals post into one request.

## Further Notes

ADR 0002 records why initialize writes missing keys instead of relying on first-access seeding or the CLI.

A parked issue describes the same gap for a whole-locale lookup that is not initialize. This spec covers initialize only. Do not close that issue as done.

The integration host does not yet fake the project push endpoint. Tests for this spec need that recording.
