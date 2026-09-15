---
status: accepted
---

# Initialize sends missing keys

Initialize used to only read locales. Translation keys reached the service through `translations push` or through a single-key lookup that seeds missing rows. An application that preloads locales at startup never looks up unused keys, so those keys never appear on the service. After a successful locale preload, initialize now sends every missing key from the application's `.resx` files, with the Neutral value and every sibling-locale value the app shipped. It does not send a key already seen in a loaded locale, it does not prune, and it does not fail startup if the send fails. First-access seeding and the CLI push stay as other paths.

## Considered options

We considered seeding each missing key with the existing single-key GET. That path never overwrites a non-empty server value, but it is one request per key and makes startup cost grow with the catalog. We considered posting the full local catalog like the CLI. That would update keys the service already has. We considered asking the service for every key in the Environment first. That would match "missing in any locale" exactly, and it needs a new API. We kept the preload snapshots as the view of what the service already has, and we send only the keys those snapshots do not contain.
