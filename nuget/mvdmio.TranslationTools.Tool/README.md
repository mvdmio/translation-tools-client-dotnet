# mvdmio.TranslationTools.Tool

CLI tool for pulling TranslationTools API translations into a C# manifest file and pushing manifest keys back to the API.

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
output: Localizations.cs
namespace: MyApp.Localization
className: Localizations
keyNaming: UnderscoreToDot
```

Relative `output` paths are resolved from the `.mvdmio-translations.yml` directory.

## Init

```bash
translations init
```

Creates `.mvdmio-translations.yml` in the current directory with starter values.

## Pull

```bash
translations pull
translations pull --overwrite
```

`translations pull` reads `.mvdmio-translations.yml`, fetches project metadata, pulls the default locale, and writes the manifest file.

By default, pull merges with an existing manifest file and preserves matching existing properties. Use `--overwrite` to replace them with incoming values.

If multiple source keys collapse to the same property under `keyNaming`, pull keeps the key that matches the configured naming policy.

The tool always uses `https://translations.mvdm.io`.

Generated manifest uses inline partial properties compatible with `mvdmio.TranslationTools.Client` source generation.
Generated manifests are always `public static partial class` and do not emit a `Culture` property.

## Push

```bash
translations push
```

`translations push` reads `.mvdmio-translations.yml`, resolves the nearest `.csproj` from the configured output path, scans the project for `[Translations]` manifests, derives keys/default values, and posts them to the TranslationTools API.

The API treats the received key set as authoritative: missing keys are removed from the project, new keys are created in the default locale, and existing default-locale values are updated from manifest `DefaultValue`.
