# Changelog

## 2026-09-15: Initialize sends missing keys

When the client initializes and every locale preload succeeds, it sends every missing key from the application's `.resx` files, with the Neutral value and any sibling-locale values the app shipped. Keys already on the service stay as they are. A failed send is logged and the application still starts.

## 2026-09-15: Illegal Environment names fail locally

The client now rejects an illegal Environment name when you construct it. The exception names the allowed characters and the 64-character limit. The CLI tool applies the same check before a push, so a name like `prod:sha` fails locally and never calls the API.
