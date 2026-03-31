# mvdmio.TranslationTools.Tool

CLI tool for migrating `.resx` files into TranslationTools, pulling TranslationTools API translations into a C# manifest file, and pushing manifest keys back to the API.

## Install

```bash
dotnet tool install --global mvdmio.TranslationTools.Tool
```

Command name: `translations`

## Quick start

```bash
translations init
translations migrate
translations pull
translations push
```

## Configuration

All configuration lives in `.mvdmio-translations.yml`.

```yaml
apiKey: project-api-key
output: Localizations.cs
namespace: MyApp.Localization
className: Localizations
keyNaming: UnderscoreToDot
defaultLocale: en
```

`.mvdmio-translations.yml` must live in the project root.
Relative `output` paths are resolved from that project root.

`defaultLocale` is optional. `translations migrate` uses it as the locale for base `Name.resx` files when present. If omitted, migrate falls back to the remote project default locale.

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
- imports all logical resource sets in one run
- keeps original `.resx` keys when the project has a single logical resource set
- prefixes API keys with the relative resource-set path and base name when the project has multiple logical resource sets
- treats base `Name.resx` files as the resolved default locale
- imports localized-only keys even when the base file does not contain them
- keeps empty locale files in project locale metadata and reports warnings
- uploads full translation state through the TranslationTools import API
- reuses pull overwrite behavior internally to regenerate the manifest from API state

Examples:

- single resource set: `Errors.resx` key `Title` -> `Title`
- multiple resource sets: `Errors.resx` key `Title` -> `Errors.Title`
- multiple resource sets: `Shared.Validation.resx` key `Required` -> `Shared.Validation.Required`
- multiple resource sets: `Admin/Labels.resx` key `Title` -> `Admin.Labels.Title`

## Pull

```bash
translations pull
translations pull --overwrite
```

`translations pull` reads `.mvdmio-translations.yml`, fetches project metadata, pulls all project locales, writes the manifest file, and writes `.mvdmio-translations.snapshot.json` in the project root.

By default, pull merges with an existing manifest file and preserves matching existing properties. Use `--overwrite` to replace them with incoming values.

If multiple source keys collapse to the same property under `keyNaming`, pull keeps the key that matches the configured naming policy.

Manifest generation uses the union of keys across all locales. Manifest `DefaultValue` comes from the default locale snapshot.

The tool always uses `https://translations.mvdm.io`.

Generated manifest uses inline partial properties compatible with `mvdmio.TranslationTools.Client` source generation.
Generated manifests are always `public static partial class` and do not emit a `Culture` property.
Generated manifests expose sync properties plus generated `GetAsync(...)` helpers at compile time.

## Push

```bash
translations push
```

`translations push` reads `.mvdmio-translations.yml`, resolves the nearest `.csproj` from the configured output path, scans the project for `[Translations]` manifests, derives keys/default values, and posts them to the TranslationTools API.

The API treats the received key set as authoritative: missing keys are removed from the project, new keys are created in the default locale, and existing default-locale values are updated from manifest `DefaultValue`.
