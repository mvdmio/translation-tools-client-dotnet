# Agent Instructions

This document provides instructions for AI coding assistants working on this codebase.

## General instructions

- Always build the solution and run the tests after making changes. Fix all build errors and test failures before finishing your work.
- Always update the README.md file so that it reflects the latest state of the project
- Keep README.md focused on stable product description, usage, and reference material. Do not add changelog-style notes, implementation history, or release-specific callouts unless the information is required for users to understand current behavior or usage.
- Always bump `TranslationToolsVersion` in `Directory.Build.props` (it versions every package) when making changes that affect a published package. Follow semantic versioning principles (MAJOR.MINOR.PATCH):
  - Increment the MAJOR version when you make incompatible API changes.
  - Increment the MINOR version when you add functionality in a backward-compatible manner.
  - Increment the PATCH version when you make backward-compatible bug fixes.

## Project Overview

.NET client, source generator, and CLI tool (`translations`) for the TranslationTools service. Glossary: `CONTEXT.md`. Decisions: `docs/adr/`. Coding standards — read before writing or reviewing code: `.agents/refs/coding-standards.md` (test conventions, comments, rules learned from mistakes).

```
src/
  mvdmio.TranslationTools.Client/                   # runtime client
  mvdmio.TranslationTools.Client.SourceGenerator/   # generates accessors + catalog from .resx
  mvdmio.TranslationTools.Tool/                     # CLI tool: init, pull, push
  mvdmio.TranslationTools.Lint/                     # never-packed comment rule (MVDM0053)
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
