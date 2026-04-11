# mvdmio.TranslationTools.Tool

Use `mvdmio.TranslationTools.Tool` to sync TranslationTools translations with local `.resx` files.

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

The tool reads its settings from `.mvdmio-translations.yml` in your project directory.

Example:

```yaml
apiKey: project-api-key
defaultLocale: en
```

- `apiKey`: your TranslationTools project API key
- `defaultLocale`: locale used for neutral `Name.resx` files

## Commands

### `translations init`

Creates a starter `.mvdmio-translations.yml` file in the current directory.

### `translations pull`

Downloads translations from TranslationTools and writes them to local `.resx` files.

```bash
translations pull
translations pull --prune
```

Use `--prune` for a destructive pull that deletes local `.resx` files and entries that no longer exist remotely.

### `translations push`

Uploads local `.resx` values to TranslationTools.

```bash
translations push
translations push --prune
```

Use `--prune` for a destructive push that deletes remote translations that no longer exist in local `.resx` files.

## Typical workflow

1. Run `translations init` once per project.
2. Set your project API key in `.mvdmio-translations.yml`.
3. Run `translations pull` to bring remote translations into local `.resx` files.
4. Edit translations locally when needed.
5. Run `translations push` to send local changes back to TranslationTools.
