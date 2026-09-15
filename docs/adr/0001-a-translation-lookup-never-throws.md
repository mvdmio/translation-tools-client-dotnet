---
status: accepted
---

# A translation lookup never throws

A translation is cosmetic, and the caller asking for one usually cannot do anything useful with a
failure. So a value lookup in the client never throws: any failure — a missing key, an
unreachable service, a `5xx`, a timeout, or a rejected API key — logs and returns the local
fallback instead. The client sets `ThrowOnLookupError` for applications that want the opposite,
mirroring the existing `ThrowOnPlaceholderError`.

## Considered options

We considered letting transport and authentication failures still throw, on the argument that a
wrong API key is a deployment mistake rather than a runtime condition and should be loud. We
rejected it for two reasons. First, it splits the contract by call site: the same outage would be
survivable through a generated property and fatal through `ITranslationToolsClient`, which is two
contracts rather than one. Second, a wrong API key is *already* silent, because
`InitializeTranslationToolsClientAsync` catches and logs every startup exception. Throwing on
lookup does not make the key loud; it only makes the application fail later and further from the
cause. Making that failure visible belongs in logging, which is why an authentication failure is
logged at `Error` while a missing translation is logged at `Warning`.

We also considered failing application startup on a `401`. We rejected it as a separate and larger
promise: it turns an outage in the translation service into a failed deployment.

## Consequences

A caller using `ITranslationToolsClient` directly passes no neutral value, so it has no local
fallback and receives a response whose `Value` is null. That is indistinguishable from a key the
service holds with no value yet, and the log is the only way to tell the two apart. We accepted
this rather than add provenance to the response type, because no caller has asked to branch on it.

Because a lookup no longer fails fast, two safeguards stop it from hanging instead. A lookup times
out after five seconds by default, and a failure that indicates the service itself is unreachable
or broken suppresses further calls for one minute. Without those, "never throws" would have become
"never returns" for a job doing dozens of lookups against a dead service.

A local fallback is never written to the cache. Cache entries have no expiry, so caching one would
let a single outage serve stale local text until the process restarted.
