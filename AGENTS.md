# Agent Instructions

This document provides instructions for AI coding assistants working on this codebase.

## General instructions

- Ask questions if you need clarification.
- Search early; quote exact errors; prefer newer sources.
- Style: telegraph. Drop filler/grammar. Min tokens (global AGENTS + replies).
- Keep files shorter than ~500 LOC; split/refactor as needed. Does not apply to test files.
- Always add tests when adding functionality.
- Always create or modify tests when fixing a bug.
- Always build the solution and run the tests after making changes. Fix all build errors and test failures before finishing your work.
- If the build fails because some process is running and locking the file, kill the process.
- Always update the README.md file so that it reflects the latest state of the project
- Keep README.md focused on stable product description, usage, and reference material. Do not add changelog-style notes, implementation history, or release-specific callouts unless the information is required for users to understand current behavior or usage.
- Always bump `TranslationToolsVersion` in `Directory.Build.props` (it versions every package) when making changes that affect a published package. Follow semantic versioning principles (MAJOR.MINOR.PATCH):
  - Increment the MAJOR version when you make incompatible API changes.
  - Increment the MINOR version when you add functionality in a backward-compatible manner.
  - Increment the PATCH version when you make backward-compatible bug fixes.

## Project Overview

.NET client, source generator, and CLI tool (`translations`) for the TranslationTools service. Glossary: `CONTEXT.md`. Decisions: `docs/adr/`. Rules learned from mistakes: `.agents/refs/coding-standards.md`.

```
src/
  mvdmio.TranslationTools.Client/                   # runtime client
  mvdmio.TranslationTools.Client.SourceGenerator/   # generates accessors + catalog from .resx
  mvdmio.TranslationTools.Tool/                     # CLI tool: init, pull, push
test/TranslationTools/                              # every test project + SourceGeneratorEndToEnd fixture
```

The service lives in the `mvdmio-suite` repo under `TranslationTools/` (sibling checkout `../mvdmio-suite`). Read its code and `TranslationTools/docs/adr/` when a change depends on how the service treats keys, push, or Environments.

## Development Workflow

### Build and Test

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Format code
dotnet format
```

### Before Committing

1. Run `dotnet build` to ensure the project compiles
2. Run `dotnet test` to verify all tests pass
3. Run `dotnet format` to format code according to .editorconfig

### Test-Driven Development

Write tests before implementing features. Tests should cover all new code.

Test projects live in `test/TranslationTools/`.

## Code Style

This project uses a comprehensive `.editorconfig` file. Key conventions:

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

### Test Infrastructure

- Use `AwesomeAssertions` for fluent assertions

### Test Naming

Use descriptive names that explain what is being tested:

```csharp
public async Task QueryAsync_WithValidSql_ReturnsResults()
public async Task BulkCopy_WithEmptyTable_CompletesSuccessfully()
```
