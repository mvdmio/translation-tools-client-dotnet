# Coding standards

How code in this repo is written: test conventions, then rules learned from mistakes. `.editorconfig` holds the formatting rules; `dotnet format` applies them.

## Testing Conventions

- Use `AwesomeAssertions` for fluent assertions.
- Use descriptive names that explain what is being tested:

```csharp
public async Task QueryAsync_WithValidSql_ReturnsResults()
public async Task BulkCopy_WithEmptyTable_CompletesSuccessfully()
```

- To assert the text of a reported error, write `.Should().ContainSingle().Which.Should().Be("…")`. The string argument of `ContainSingle("…")` is the failure message, not the expected value, so that call passes whatever the item says.
- A test that reads process-wide static state, such as the catalog registry, sets that state up itself. Other tests call `Clear`, so a test that relies on a module initializer having run fails depending on test order.

## Comments

- Code carries no comments ([ADR 0004](../../docs/adr/0004-code-carries-no-comments.md)). The build fails on a comment with `MVDM0053`.
- A `///` doc comment stays only on a public or protected member of the Client (`mvdmio.TranslationTools.Client`). It fails anywhere else.
- A justified `#pragma warning disable` carries its reason at the end of the pragma line, never on the line above.

## Translation keys

- Compare translation keys without regard to case: key the collection on `TranslationRef`, or use `StringComparer.OrdinalIgnoreCase`. The service treats keys that differ only in case as one key; an exact comparison makes the client miss its own cache and call the service on every lookup. The CLI tool's push matches keys without regard to case and sends the default-locale file's casing. The source generator matches locale `.resx` keys to neutral keys without regard to case and uses the neutral file's casing. Three places still compare keys exactly: pull's change summary (`PullHandler.CalculateChangeSummary`), the `.resx` parser's duplicate-key check (`ResxResourceSetParser`), and the generated local fallback table (`_localeValuesByKey`, emitted by `TranslationManifestEmitter`). The generated properties query that table with the neutral key's own casing, but the generated `Get(string key, ...)` and `GetAsync(string key, ...)` miss the local fallback for a key passed in another casing. Changing them is a separate decision.
