# Validate environment names locally

Status: ready-for-agent

## Problem Statement

A developer can set an Environment name that the TranslationTools API will never accept. A colon is one such character. The API answers those requests with HTTP 400 and a message that names the allowed characters.

The client library hides that answer. The heartbeat treats the 400 as a transient failure. It logs a warning and retries on the next tick. Startup initialization also catches the same 400 when it pushes global placeholders. It logs an error and lets the host start. Translation lookups then run as if the name were unknown. The deployment never joins the Environment the developer named.

The developer has no signal that the name itself is the problem. They asked the library to use that Environment, and the library agreed.

## Solution

The client rejects an illegal Environment name before it sends any request. Constructing the client with such a name throws. The exception names the allowed characters so the developer can fix the configuration.

Blank or whitespace still means the unnamed Environment. A legal name is unchanged: it is trimmed and sent as today.

The CLI tool applies the same check before it pushes. It prints an error that names the allowed characters and does not call the API.

A heartbeat that fails for any other reason stays best-effort. A translation lookup still never throws because of this change.

## User Stories

1. As a developer, I want constructing the client with `Environment = "prod:sha"` to throw, so that I learn the colon is illegal before the process starts talking to the API.
2. As a developer, I want the thrown exception to name the allowed characters and the length limit, so that I can pick a legal name without reading the API source.
3. As a developer, I want constructing the client with `Environment = "production"` to succeed, so that a normal deployment name still works.
4. As a developer, I want constructing the client with `Environment = "staging"` to succeed, so that a second common name still works.
5. As a developer, I want constructing the client with `Environment` left unset to succeed, so that a deployment that does not name an Environment still uses the unnamed Environment.
6. As a developer, I want constructing the client with `Environment = ""` to succeed, so that a blank name still means the unnamed Environment.
7. As a developer, I want constructing the client with `Environment = "   "` to succeed, so that whitespace-only still means the unnamed Environment.
8. As a developer, I want constructing the client with `Environment = " production "` to succeed and send `production`, so that incidental padding does not become an illegal name.
9. As a developer, I want constructing the client with `Environment = "Production"` to succeed, so that mixed case still works and the server can lowercase it as it already does.
10. As a developer, I want constructing the client with `Environment = "dev_local"` to succeed, so that an underscore remains legal.
11. As a developer, I want constructing the client with `Environment = "build-123"` to succeed, so that a hyphen remains legal.
12. As a developer, I want constructing the client with `Environment = "v1.2"` to succeed, so that a dot inside a name remains legal.
13. As a developer, I want constructing the client with `Environment = "."` to throw, so that a name that would vanish as a URL path segment cannot be configured.
14. As a developer, I want constructing the client with `Environment = ".."` to throw, so that the other reserved path segment cannot be configured.
15. As a developer, I want constructing the client with a 64-character legal name to succeed, so that the documented maximum is accepted.
16. As a developer, I want constructing the client with a 65-character name to throw, so that an overlong name fails here rather than as an HTTP 400 later.
17. As a developer, I want constructing the client with `Environment = "prod env"` to throw, so that a space is rejected the same way a colon is.
18. As a developer, I want constructing the client with `Environment = "prod!"` to throw, so that punctuation outside the allowed set is rejected.
19. As a developer, I want constructing the client with `Environment = "prod/east"` to throw, so that a slash, which would break the path segment, is rejected.
20. As a developer, I want an illegal Environment to fail even when heartbeat is disabled, so that a name I put on pull URLs is still checked.
21. As a developer, I want an illegal Environment to fail even when I have not registered global placeholders, so that I do not depend on a globals push to surface the mistake.
22. As a developer, I want no heartbeat request to leave the process when construction failed, so that a bad name cannot be retried as if the service were down.
23. As a developer, I want no translation pull to leave the process when construction failed, so that lookups cannot silently run against the unknown-Environment fallback.
24. As a developer who constructs the client myself, I want the failure as an exception I can catch in tests and startup, so that I do not have to scrape logs to learn the name is wrong.
25. As a developer who uses the host initializer, I want that initializer to keep catching construction failures and logging them at Error, so that a bad Environment is at least as visible as a bad DefaultLocale and does not take down the host by a new rule.
26. As a developer, I want a heartbeat 500 to stay a warning that retries, so that a real outage is still best-effort after this change.
27. As a developer, I want a single-key lookup that hits a down service to keep returning local fallback, so that this change does not reopen the lookup failure contract.
28. As a developer using the CLI tool, I want `environment: prod:sha` in `.mvdmio-translations.yml` to fail the push with a message that names the allowed characters, so that I do not have to decode an HTTP 400.
29. As a developer using the CLI tool, I want that failed push not to call the API, so that a config mistake is not a rejected request.
30. As a developer using the CLI tool, I want `environment: production` to push as it does today, so that a legal name is unaffected.
31. As a developer using the CLI tool, I want omitting `environment` to push into the unnamed Environment as it does today.
32. As a developer reading the client package docs, I want the Environment option to state the allowed characters, the length limit, and that blank means unnamed, so that I can choose a legal name without a failed start.
33. As a developer reading the CLI tool docs, I want the `environment` setting to state the same rules, so that the name I push matches the name the runtime client will accept.
34. As a developer reading the glossary, I want Environment to say what characters a set name may use, so that the domain language and the check agree.
35. As a developer who already ships a legal Environment, I want no change to the requests the client sends, so that this is a check on bad input rather than a new wire format.

## Implementation Decisions

- The allowed Environment name is the same rule the API already enforces: after trim, a set name is at most 64 characters, matches letters, digits, `.`, `_`, or `-` only, and is not `.` or `..`. Blank or whitespace is the unnamed Environment and is legal.
- The client library applies that check when it is constructed, next to the existing ApiKey and DefaultLocale checks. An illegal name throws `ArgumentException`. The message names the allowed characters and the length limit.
- The client does not lowercase the name. The server already does that. Trim stays as it is today.
- The CLI tool applies the same check before it sends a push. It prints the error and does not call the API. A legal or blank name is unchanged.
- Prefer one shared check used by both packages so the library and the tool cannot drift from each other. The allowed characters must stay identical to the API's rule. Do not invent a second charset.
- Do not change heartbeat best-effort handling. An illegal name never reaches the heartbeat, because construction already failed. Other heartbeat failures still log a warning and retry.
- Do not change the host initializer's catch-and-log of startup failures. Construction throw plus that existing Error log is the same pattern DefaultLocale already uses.
- Do not change the lookup failure contract. A bad Environment is a configuration error, not a failed lookup.
- Do not add an option to allow illegal names.
- Document the allowed characters, the length limit, `.` / `..`, and blank-means-unnamed on the Environment option in the client package docs and on the `environment` setting in the CLI tool docs.
- Sharpen the Environment glossary entry so a set name's allowed characters are part of the term, not only of the check.
- Bump the package version as a patch. This is a bug fix: an illegal name already failed at the API, and the client now reports that instead of hiding it.

## Testing Decisions

A good test asserts what a caller sees: constructing the client, or running a tool command, either succeeds or fails with a message about the Environment name. Tests do not assert the shape of a helper, a regex, or which private method ran.

Test the client through construction, the same way ApiKey and DefaultLocale are already tested.

Cover at least:

- a colon in the name throws
- a space, a slash, and other illegal punctuation throw
- `.` and `..` throw
- 65 characters throw
- 64 legal characters succeed
- `production`, mixed case, underscore, hyphen, and an interior dot succeed
- null, empty, and whitespace succeed
- surrounding whitespace on a legal name succeeds
- construction with an illegal name performs no HTTP request

Test the CLI tool through the push command's configuration input. An illegal `environment` value reports an error and does not call the API. A legal value and an omitted value still push.

Do not add tests that the heartbeat retry still swallows a 500; that behaviour is already covered and is out of scope to change.

Prior art: the construction tests that reject a blank ApiKey and an illegal DefaultLocale; the heartbeat tests that append a legal Environment to a pull and that keep a failed heartbeat from throwing out of initialization.

## Out of Scope

- Changing the API's Environment name rules, the 400 body, or the 100-named-Environment cap.
- Failing host startup for translation-service outages, rejected API keys, or other initializer errors.
- Changing how a pull treats an unknown Environment name (it still serves the full key set).
- Validating Environment again on every heartbeat or lookup after a successful construction.
- Per-environment translated values, per-environment API keys, or any other Environment behaviour beyond the name check.
- The CLI tool's pull path, which does not send an Environment name today.

## Further Notes

This spec assumes the Environment option, the heartbeat, and the CLI `environment` setting that already exist. It does not add those features.

The API rejects an Environment name that is not a single URL path segment in the translation-key charset. A colon is the example that prompted this work. An Origin already uses a colon as `<project>:<path>`. A developer can put a colon in an Environment name and have that look right. The API then refuses it.

The host initializer will still catch the new `ArgumentException` and log it at Error, then continue. That is existing behaviour. It is not a hole this spec leaves open. Callers who construct the client themselves receive the exception. Callers who use the initializer receive the Error log. Both are louder than today's heartbeat warning.
