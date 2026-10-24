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
defaultLocale: en
environment: production # optional
apiKey: project-api-key # optional, see below
```

- `apiKey` (optional): your TranslationTools project API key. When the file has no `apiKey`, the tool reads the key from the `TRANSLATIONS_API_KEY` environment variable. A key in the file wins over the variable. If neither holds a key, `translations pull` and `translations push` stop and name both places. Leave `apiKey` out of a committed file and set the variable instead.
- `defaultLocale`: locale used for neutral `Name.resx` files
- `environment` (optional): deployment environment name, e.g. `production` or `staging`. When set, `translations push` declares your local key set into that environment's membership on the server, scoping which keys belong to this deployment. Blank, whitespace, or omitted means the unnamed environment. A set name may use letters, digits, `.`, `_`, or `-` only, must be at most 64 characters after trim, and must not be `.` or `..`. An illegal name fails the push before any API call. Leave it unset unless you maintain separate key sets per environment.

## Commands

### `translations init`

Creates a starter `.mvdmio-translations.yml` file in the current directory. The file has no `apiKey`; set `TRANSLATIONS_API_KEY` or add `apiKey` to the file.

### `translations pull`

Downloads translations from TranslationTools and writes them to local `.resx` files.

```bash
translations pull
translations pull --prune
```

- Matches the stored origin project segment to the local `.csproj` name case-insensitively (`mvdmio.localization` matches `mvdmio.Localization`).
- Writes locale `.resx` files using the casing of an existing local resource set (`Localizations.nl.resx` stays `Localizations.nl.resx` when the stored origin is `/localizations.resx`).
- Does not overwrite the default-locale / neutral `.resx` (the authored source file).
- Writes `.mvdmio-translations.pull.json` in the project directory as the baseline for the next push.

Use `--prune` for a destructive pull that deletes local `.resx` files and entries that no longer exist remotely.

### `translations push`

Uploads local `.resx` values to TranslationTools.

```bash
translations push
translations push --prune
```

Push matches translation keys within one origin without regard to case, as the service does. It sends one item per key and locale, so `Action.Edit` in `Localizations.resx` and `action.edit` in `Localizations.nl.resx` push as one key. Every item for a key uses the default-locale file's casing. A key found only in locale files uses the casing of the locale file whose locale name sorts first.

When `.mvdmio-translations.pull.json` is present from a previous pull:

- A locale value that changed since that pull is posted as a write (the API treats that as a human edit and clears the machine-filled mark).
- An unchanged pulled locale value is posted as `null`, so the server keeps the current text and its machine-filled mark.
- Default-locale values already present in the pull baseline are posted as `null`, so a sparse or stale local source file cannot overwrite live English.

Without a pull baseline, push still posts every local value (bootstrap / first upload).

Use `--prune` for a destructive push that deletes remote translations that no longer exist in local `.resx` files.

## Typical workflow

1. Run `translations init` once per project.
2. Set your project API key in the `TRANSLATIONS_API_KEY` environment variable, or as `apiKey` in `.mvdmio-translations.yml`.
3. Run `translations pull` to bring remote translations into local `.resx` files.
4. Edit translations locally when needed.
5. Run `translations push` to send local changes back to TranslationTools.
