# 02 — Reject illegal Environment names on CLI push

Status: pending
Blocked by: 01

## What to build

`translations push` rejects an illegal `environment` in `.mvdmio-translations.yml` before it calls the API. A developer who wrote `environment: prod:sha` sees an error that names the allowed characters and the length limit; they do not have to decode an HTTP 400.

Use the shared check from step 01. Do not invent a second charset. Print the error on the existing push reporter path (the same way a missing API key fails) and return without throwing out of the handler, so the API is not called. A legal `environment: production` still pushes as today. Omitting `environment` still pushes into the unnamed Environment as today. Blank or whitespace still means unnamed.

Document the same rules on the CLI `environment` setting: allowed characters, length limit, `.` / `..`, and blank-means-unnamed.

Do not change the CLI pull path. Do not change heartbeat best-effort handling or the lookup failure contract. This step leaves the whole suite green.

## Footprint

Projects: mvdmio.TranslationTools.Client, mvdmio.TranslationTools.Client.SourceGenerator, mvdmio.TranslationTools.Tool, mvdmio.TranslationTools.Client.Tests.Unit, mvdmio.TranslationTools.Client.Tests.Integration, mvdmio.TranslationTools.Tool.Tests.Unit, SourceGeneratorEndToEnd

- `src/mvdmio.TranslationTools.Tool/Push/PushHandler.cs` — `HandleAsync`, `IPushReporter`
- `src/mvdmio.TranslationTools.Tool/Configuration/ToolConfiguration.cs` — `Environment`
- `src/mvdmio.TranslationTools.Tool/README.md` — `environment` setting
- `test/TranslationTools/mvdmio.TranslationTools.Tool.Tests.Unit/Push/PushHandlerTests.cs` — configured Environment on push, `TestTranslationApiService`, `TestPushReporter`

## Acceptance criteria

- [ ] Push with `environment: prod:sha` reports an error that names the allowed characters and does not call the API.
- [ ] Push with `environment: production` still sends that Environment as today.
- [ ] Push with `environment` omitted still pushes into the unnamed Environment as today.
- [ ] CLI tool docs state the allowed characters, the length limit, `.` / `..`, and that blank means unnamed.
- [ ] The whole suite is green.
