# Coding standards

`AGENTS.md` holds the code style. This file holds rules learned from mistakes.

## Translation keys

- Compare translation keys without regard to case: key the collection on `TranslationRef`, or use `StringComparer.OrdinalIgnoreCase`. The service treats keys that differ only in case as one key; an exact comparison makes the client miss its own cache and call the service on every lookup. The CLI tool's push matches keys without regard to case and sends the default-locale file's casing. Three places still compare keys exactly: the source generator, pull's change summary (`PullHandler.CalculateChangeSummary`), and the `.resx` parser's duplicate-key check (`ResxResourceSetParser`). Changing them is a separate decision.
