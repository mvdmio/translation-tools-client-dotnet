# Coding standards

How code in this repo is written: code style, then rules learned from mistakes. `.editorconfig` holds the formatting rules; `dotnet format` applies them.

## Code style

### Formatting

- **Indentation:** 3 spaces for C# files
- **Namespace style:** File-scoped (`namespace Foo;`)
- **Line endings:** LF
- **Final newline:** Required

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Methods, Properties | PascalCase | `DatabaseConnection` |
| Private fields | _camelCase | `_connection` |
| Parameters, Variables | camelCase | `connectionString` |
| Constants | UPPER_SNAKE_CASE | `DEFAULT_TIMEOUT` |
| Interfaces | IPrefix | `IDbMigration` |
| Generic parameters | TPrefix | `TEntity` |
| Async methods | Async suffix | `ExecuteAsync` |

### Code Preferences

- Use `var` when type is apparent
- Use file-scoped namespaces
- Use braces for multi-line statements
- Prefer pattern matching
- Use null propagation (`?.`) and null coalescing (`??`)

## Documentation

All public methods must have XML documentation comments:

```csharp
/// <summary>
/// Executes a SQL query and returns the results.
/// </summary>
/// <typeparam name="T">The type to map results to.</typeparam>
/// <param name="sql">The SQL query to execute.</param>
/// <param name="parameters">Optional query parameters.</param>
/// <returns>An enumerable of mapped results.</returns>
public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
```

## Testing Conventions

- Use `AwesomeAssertions` for fluent assertions.
- Use descriptive names that explain what is being tested:

```csharp
public async Task QueryAsync_WithValidSql_ReturnsResults()
public async Task BulkCopy_WithEmptyTable_CompletesSuccessfully()
```

- To assert the text of a reported error, write `.Should().ContainSingle().Which.Should().Be("…")`. The string argument of `ContainSingle("…")` is the failure message, not the expected value, so that call passes whatever the item says.
- A test that reads process-wide static state, such as the catalog registry, sets that state up itself. Other tests call `Clear`, so a test that relies on a module initializer having run fails depending on test order.

## Translation keys

- Compare translation keys without regard to case: key the collection on `TranslationRef`, or use `StringComparer.OrdinalIgnoreCase`. The service treats keys that differ only in case as one key; an exact comparison makes the client miss its own cache and call the service on every lookup. The CLI tool's push matches keys without regard to case and sends the default-locale file's casing. The source generator matches locale `.resx` keys to neutral keys without regard to case and uses the neutral file's casing. Three places still compare keys exactly: pull's change summary (`PullHandler.CalculateChangeSummary`), the `.resx` parser's duplicate-key check (`ResxResourceSetParser`), and the generated local fallback table (`_localeValuesByKey`, emitted by `TranslationManifestEmitter`). The generated properties query that table with the neutral key's own casing, but the generated `Get(string key, ...)` and `GetAsync(string key, ...)` miss the local fallback for a key passed in another casing. Changing them is a separate decision.
