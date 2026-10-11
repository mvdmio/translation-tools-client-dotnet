---
status: accepted
---

# Initialize sends the Declaration

Translation keys used to reach the service only through `translations push`, or through a single-key lookup that seeds missing rows. An application that preloads locales never looks up unused keys, so those keys never appeared on the service. So Initialize first sends the application's Declaration under its Environment: every key in its `.resx` files, with the Neutral value and every sibling-locale value. Then it preloads. The service makes those keys the Environment's complete set and stores the values by Seeding, so a restart never overwrites a value edited in TranslationTools. The client collects keys from every assembly the application depends on, not only from assemblies whose code has already run. A failed Declaration is logged and does not fail startup.

## Considered options

- **Send only the keys the preload did not return** (3.6.0 to 3.8.x). Rejected: the service replaces an Environment's key set with each push. Under an Environment name that lasts across releases, the set swapped halves on every start, and the service deleted the dropped half a day later. A key the service held outside the Environment's preload also counted as missing, and the send overwrote its edited values.
- **Post the full catalog to the push route, like the CLI.** Rejected: the push overwrites every value that differs, so every start would undo edits made in TranslationTools.
- **Ask the service first which keys the Environment declares.** Rejected: it needs a new API and a second round trip, and the write still needs a rule that never overwrites.
- **Seed each key with the single-key GET.** Rejected: one request per key, so startup cost grows with the catalog.

## Consequences

- The Declaration has its own service route. A service without it answers 404; the client logs that, and nothing is overwritten.
- Changed text for an existing key reaches the service through `translations push`, or as a new key. A restart never sends it.
- Every start sends the whole catalog in one request, whose size grows with the catalog.
