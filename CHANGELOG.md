# Changelog

## 2026-10-21: Failed startup request keeps the heartbeat and live updates

A request that fails while the application starts, such as a `502` from TranslationTools during a deploy, now costs a logged warning and no longer stops the rest of startup. A locale that cannot be preloaded is logged by name, the next locale is still preloaded, and a failed push of the global placeholders is logged as well. The heartbeat and live updates start anyway, and a locale that failed to preload is fetched by the first lookup that needs it. Before, the first failed request stopped startup: the Environment sent no heartbeat and received no live updates until the application restarted.

## 2026-10-11: Initialize keeps Environment keys

When the client initializes, it now sends every translation key from the application's `.resx` files to the Environment, before it loads the locales. A name like `production` no longer loses half its keys on every second start, and values edited in TranslationTools are no longer overwritten. Keys from `.resx` files in class libraries are included too, even when no code from the library has run yet.

## 2026-10-10: Generator keeps differently cased locale values

The source generator now matches translation keys without regard to case. When a locale `.resx` file spells a key in a different case than the default-locale file, the generated local fallback holds that locale's value, and the catalog holds one entry with every value under the default-locale file's casing. Users see the translated text when the service cannot answer, and initialize sends every value for a missing key.

## 2026-10-10: Push keeps differently cased locale values

`translations push` now matches translation keys without regard to case. When a locale `.resx` file spells a key in a different case than the default-locale file, push sends that locale's value instead of dropping it, under the default-locale file's casing. Each key is sent once per locale, so the push count matches what the service receives.

## 2026-10-10: Keys match without regard to case

The client now matches translation keys without regard to case, as the service does. A lookup for `Action.Edit` is answered from a cached `action.edit`, so no call goes to the service and rendering does not wait on one. The value that comes back holds the key in the casing the service stores.

## 2026-09-15: Initialize sends missing keys

When the client initializes and every locale preload succeeds, it sends every missing key from the application's `.resx` files, with the Neutral value and any sibling-locale values the app shipped. Keys already on the service stay as they are. A failed send is logged and the application still starts.

## 2026-09-15: Illegal Environment names fail locally

The client now rejects an illegal Environment name when you construct it. The exception names the allowed characters and the 64-character limit. The CLI tool applies the same check before a push, so a name like `prod:sha` fails locally and never calls the API.
