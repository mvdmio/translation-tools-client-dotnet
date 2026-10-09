# Coding standards

`AGENTS.md` holds the code style. This file holds rules learned from mistakes.

## Translation keys

- Compare translation keys without regard to case: key the collection on `TranslationRef`, or use `StringComparer.OrdinalIgnoreCase`. The service treats keys that differ only in case as one key; an exact comparison makes the client miss its own cache and call the service on every lookup. The source generator and the CLI tool still compare keys exactly; changing them is a separate decision.
