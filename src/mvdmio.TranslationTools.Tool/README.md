# mvdmio.TranslationTools.Tool

CLI tool for syncing project `.resx` files with TranslationTools.

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

`defaultLocale` is required. Base `Name.resx` files are treated as that locale.

## Init

```bash
translations init
```

Creates `.mvdmio-translations.yml` in the current directory with starter values.

## Pull

```bash
translations pull
translations pull --prune
```

`translations pull` reads `.mvdmio-translations.yml`, fetches project metadata, pulls all project locales, and writes `.resx` files into the project tree.

By default, pull adds and updates local `.resx` entries but does not delete local content.

Use `--prune` to remove local entries and locale files that no longer exist remotely.

Pull preserves valid `.resx` structure and existing entry comments for entries that remain.

The tool always uses `https://translations.mvdm.io`.

## Push

```bash
translations push
```

`translations push` reads `.mvdmio-translations.yml`, scans project `.resx` files, derives project translation state, and posts it to the TranslationTools import API.

Push is authoritative for the explicit project `.resx` state you send.
