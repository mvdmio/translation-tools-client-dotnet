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

## Translation keys

- Compare translation keys without regard to case: key the collection on `TranslationRef`, or use `StringComparer.OrdinalIgnoreCase`. The service treats keys that differ only in case as one key; an exact comparison makes the client miss its own cache and call the service on every lookup. The CLI tool's push matches keys without regard to case and sends the default-locale file's casing. Three places still compare keys exactly: the source generator, pull's change summary (`PullHandler.CalculateChangeSummary`), and the `.resx` parser's duplicate-key check (`ResxResourceSetParser`). Changing them is a separate decision.
