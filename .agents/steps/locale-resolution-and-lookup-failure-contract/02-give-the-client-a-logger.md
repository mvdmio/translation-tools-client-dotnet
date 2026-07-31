# 02 — Give the client a logger

Status: done

Prefactor. No user-visible behaviour beyond where an existing best-effort message is written. It
exists so the later steps have somewhere to log a degraded lookup, and so log assertions in those
steps are behavioural rather than reaching into diagnostics plumbing.

## What to build

`TranslationToolsClient` can log. It takes an optional `ILogger`, supplied from the container the
same way the placeholder runtime already receives one at startup. Without a logger, nothing is
logged and nothing throws.

The heartbeat's best-effort failure message moves off `Trace` onto that logger, so the client has one
way of reporting a problem rather than two.

The test project gains the one new test-side helper this feature needs: a fake `ILogger` that records
level and message, so later steps can assert on what an operator would see.

## Acceptance criteria

- [ ] `TranslationToolsClient` accepts an optional `ILogger`, and the service registration supplies
      one from the application's logger factory.
- [ ] A failing heartbeat writes one line through the supplied logger instead of to `Trace`, and
      still never surfaces as an exception in application code.
- [ ] A client constructed without a logger behaves exactly as it does today and never throws.
- [ ] A fake `ILogger` recording level and message lives in the test project and is exercised by the
      heartbeat test.
- [ ] No other behaviour changes, and the full test suite is green.

## Outcome

`TranslationToolsClient` now takes an optional `ILogger`. Both constructors gained it: the public
one as `ILogger<TranslationToolsClient>? logger = null`, and the internal one used by tests as a
plain `ILogger? logger = null` appended after the existing `clientIdStore` parameter, so every
existing positional call site (production and test) kept compiling unchanged.

The service registration in `DependencyInjectionExtensions.AddTranslationToolsClient` resolves
`ILoggerFactory` from the container the same way `ConfigurePlaceholderRuntime` already does for
`PlaceholderRuntime`, calls `CreateLogger<TranslationToolsClient>()`, and passes the result into the
internal constructor. A container with no registered `ILoggerFactory` yields a null logger, which
the client already tolerates.

`SafeSendHeartbeatAsync`'s catch block no longer calls `Trace.WriteLine`; it calls
`_logger?.LogWarning(exception, "TranslationTools heartbeat failed.")`. The `using
System.Diagnostics;` import was removed since nothing else in the file used it. Warning was chosen
over Error because a heartbeat is a liveness ping, not a lookup — the spec's Error/Warning split
(rejected API key vs. everything else) applies to lookup failures in a later step, and a failing
heartbeat retries silently either way.

Added `test/TranslationTools/mvdmio.TranslationTools.Client.Tests.Unit/FakeLogger.cs`: a standalone
`FakeLogger : ILogger` that records each call as a `FakeLogEntry(LogLevel, string Message,
Exception? Exception)` in a `ConcurrentBag`, matching the concurrency the heartbeat's background
loop and other future concurrent callers need. It's a plain top-level class (not nested in one test
file) since later steps in this spec are expected to reuse it for lookup-failure log assertions.

Two tests added to `HeartbeatAndEnvironmentTests`:
- `Heartbeat_FailingEndpoint_ShouldLogThroughSuppliedLogger_InsteadOfThrowing` — a `500` heartbeat
  response with a `FakeLogger` supplied logs exactly one `Warning` entry whose message contains
  "heartbeat", and `Initialize` still doesn't throw.
- `Heartbeat_FailingEndpoint_ShouldNotThrow_WhenNoLoggerSupplied` — the existing no-logger behaviour
  (construct without a logger, `Initialize` never throws on a failing heartbeat) is still exercised
  explicitly now that logging is an option rather than the only path.

`HeartbeatAndEnvironmentTests.CreateClient` gained an optional `logger` parameter, and a
`WaitForLogEntryAsync` polling helper was added alongside the file's existing
`WaitForHeartbeatsAsync`/`AdvanceUntilHeartbeatsAsync` helpers, since the logger call happens
slightly after the heartbeat request is recorded (both run inside the same catch block, but the
test needed to avoid a race against the background heartbeat loop).

Full solution build and `dotnet test` are green: 93 unit tests (91 + 2 new), 7 integration tests, 19
tool unit tests — no failures, no regressions.

No deviations from the step or spec.
