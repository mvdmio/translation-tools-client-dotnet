# 01 — Reject illegal Environment names at client construction

Status: pending
Blocked by: none

## What to build

Constructing the client with an illegal Environment name throws before any request leaves the process. A developer who sets `Environment = "prod:sha"` learns immediately that the colon is illegal; they do not get a heartbeat warning, a globals-push error, or lookups that run as if the name were unknown.

The allowed Environment name is the rule the API already enforces, and the same charset this client already uses for translation keys: after trim, a set name is at most 64 characters, matches letters, digits, `.`, `_`, or `-` only, and is not `.` or `..`. Blank or whitespace is the unnamed Environment and is legal. The client does not lowercase the name. Trim-on-send stays as it is today.

The throw is `ArgumentException`. The message names the allowed characters and the 64-character length limit. The check runs when the client is constructed, next to the existing ApiKey and DefaultLocale checks, so it applies even when heartbeat is disabled and even when no global placeholders are registered. A recording HTTP handler sees no requests when construction failed.

Legal names still construct: `production`, `staging`, mixed case, underscore, hyphen, an interior dot, surrounding whitespace on a legal name, and a 64-character legal name. Null, empty, and whitespace-only still construct. A legal Environment does not change the requests the client sends.

Put the rule in one shared check the CLI tool can call in the next step, so the library and the tool cannot drift. Do not invent a second charset. Do not add an option that allows illegal names. Do not change heartbeat best-effort handling, the host initializer's catch-and-log, or the lookup failure contract (ADR-0001).

Document the allowed characters, the length limit, `.` / `..`, and blank-means-unnamed on the Environment option in the client package docs, and sharpen the Environment glossary entry so a set name's allowed characters are part of the term. Bump `TranslationToolsVersion` as a patch (this is a bug fix: an illegal name already failed at the API).

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration

- `src/mvdmio.TranslationTools.Client/TranslationToolsClient.cs` — constructor (ApiKey / DefaultLocale checks), `NormalizedEnvironment`
- `src/mvdmio.TranslationTools.Client/Internal/TranslationClientInputValidator.cs` — existing translation-key charset; shared Environment check
- `src/mvdmio.TranslationTools.Client/TranslationToolsClientOptions.cs` — `Environment`
- `src/mvdmio.TranslationTools.Client/mvdmio.TranslationTools.Client.csproj` — InternalsVisibleTo (tool already references this project)
- `src/mvdmio.TranslationTools.Client/Properties/AssemblyInfo.cs` — InternalsVisibleTo
- `src/mvdmio.TranslationTools.Client/Readme.md` — Environment option table, Environment scoping
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/TranslationToolsClientConstructionTests.cs` — ApiKey / DefaultLocale construction throws
- `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/HeartbeatAndEnvironmentTests.cs` — legal Environment on pull/heartbeat, `RecordingHandler`
- `CONTEXT.md` — Environment
- `Directory.Build.props` — `TranslationToolsVersion`

## Acceptance criteria

- [ ] Constructing the client with `Environment = "prod:sha"` throws `ArgumentException` whose message names the allowed characters and the length limit.
- [ ] Constructing with a space, a slash, `prod!`, `.`, `..`, or 65 characters throws the same way.
- [ ] Constructing with `production`, `staging`, `Production`, `dev_local`, `build-123`, `v1.2`, ` production `, or 64 legal characters succeeds.
- [ ] Constructing with Environment unset, `""`, or `"   "` succeeds.
- [ ] Construction with an illegal name performs no HTTP request, including when heartbeat is disabled and when no global placeholders are registered.
- [ ] A legal Environment is still trimmed and sent as today; the client does not lowercase it.
- [ ] Client package docs and the Environment glossary state the allowed characters, the length limit, `.` / `..`, and that blank means unnamed.
- [ ] One shared check exists for the tool to call; package version is bumped as a patch.
- [ ] Footprint projects are green.
