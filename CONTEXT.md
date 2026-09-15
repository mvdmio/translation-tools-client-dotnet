# TranslationTools .NET Client

The .NET client and CLI tool for the TranslationTools service. An application declares its
translatable strings in `.resx` files, pushes them to the service, and reads translated values
back at runtime. This file is the glossary for that domain.

## Language

**Translation key**:
The name a translatable string is known by, unique within its origin. Written in `.resx` as the
`data` element's `name`.
_Avoid_: resource name, string id

**Origin**:
The `.resx` file a translation key came from, written as `<project>:<path>`. Two keys with the
same name in different files are different translations.
_Avoid_: resource set, source, namespace

**Neutral value**:
The value a translation key has in its `.resx` file with no locale suffix. It is the text the
developer wrote, not a translation of anything.
_Avoid_: default value, fallback text, source string

**Effective locale**:
The locale a lookup actually runs against, after the client has replaced a locale it cannot use.
A caller may pass the invariant culture, whose name is empty; the effective locale is what the
client substitutes for it.
_Avoid_: resolved culture, target locale

**Local fallback**:
The value a lookup returns when the service cannot answer it, taken from the `.resx` data the
source generator embedded in the application. A local fallback is real text the application
shipped with, not a placeholder.
_Avoid_: offline value, cached value, degraded value

**Environment**:
A name a deployment reports itself under, such as `production` or `staging`, so the service can
scope which translation keys it serves to that deployment.
_Avoid_: stage, tier, ring

**Global placeholder**:
A `{token}` whose value the client resolves on every render from application state, rather than
from an argument at the call site.
_Avoid_: ambient token, context variable

**Key-scoped token**:
A `{token}` whose value the caller supplies per call, as a parameter on the generated accessor.
_Avoid_: local placeholder, argument token
