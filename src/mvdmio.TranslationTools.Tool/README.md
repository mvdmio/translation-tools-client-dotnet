# mvdmio.TranslationTools.Tool

CLI tool for migrating local `.resx` files into TranslationTools, pulling remote origin-aware translation state into local `.resx` files, and pushing local `.resx` values back to the API.

## Install

```bash
dotnet tool install --global mvdmio.TranslationTools.Tool
```

Command name: `translations`

## Quick start

```bash
translations init
translations pull
translations push
```

## Configuration

All configuration lives in `.mvdmio-translations.yml`.

```yaml
apiKey: project-api-key
defaultLocale: en
```

`.mvdmio-translations.yml` must live in the project root.

`defaultLocale` is required for current push/migrate flows. Neutral `Name.resx` files map to that locale.

## Init

```bash
translations init
```

Creates `.mvdmio-translations.yml` in the current directory with starter values.

## Migrate

```bash
translations migrate
```

`translations migrate` requires `.mvdmio-translations.yml` to exist already. If configuration is missing, run `translations init` first.

Current migrate behavior:

- resolves the nearest `.csproj` from the configured output path
- scans all `.resx` files under that project, excluding `bin/` and `obj/`
- derives `Origin` from the normalized project-relative base `.resx` path
- treats neutral `Name.resx` files as the configured `defaultLocale`
- imports sparse origin-aware translation rows through the TranslationTools import API
- imports localized-only keys even when the neutral file does not contain them
- keeps empty locale files in project locale metadata and reports warnings
- refreshes local `.resx` files from API state after import

Examples:

- `Errors.resx` key `Title` -> origin `/Errors.resx`, key `Title`
- `Shared.Validation.resx` key `Required` -> origin `/Shared.Validation.resx`, key `Required`
- `Admin/Labels.resx` key `Title` -> origin `/Admin/Labels.resx`, key `Title`

## Pull

```bash
translations pull
translations pull --prune
```

`translations pull` reads `.mvdmio-translations.yml`, fetches project metadata, pulls all project locales, writes local `.resx` files by explicit origin, and writes `.mvdmio-translations.snapshot.json` in the project root.

Current behavior:

- creates missing local files for remote origins
- writes locale-specific `.resx` files only when that locale has values for the origin
- uses the union of fetched locale rows to write local files
- current implementation no longer treats dotted keys as file paths
- current `--prune` command surface exists, but full remote-aligned deletion is not fully implemented yet

## Push

```bash
translations push
translations push --prune
```

`translations push` reads `.mvdmio-translations.yml`, resolves the nearest `.csproj` from the configured output path, scans the project for local `.resx` files, derives origin-aware translation rows, and posts them to the TranslationTools API.

Current behavior:

- scans `.resx` files directly
- sends exact local entry names as `Key`
- sends the union of keys across each resource set's locale files
- emits missing locale keys as `null` values so sparse locale files still clear/create rows remotely
- requires `defaultLocale` in config
- command surface exposes `--prune`
- scanner still includes a legacy manifest fallback only when no `.resx` files exist

## Snapshot and generated client flow

- `translations pull` writes `.mvdmio-translations.snapshot.json`
- snapshot is consumed by `mvdmio.TranslationTools.Client`
- runtime source generation now reads neutral `.resx` files directly
- older `[Translations]` manifest scaffolding remains only in compatibility/test paths
