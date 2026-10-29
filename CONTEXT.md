# TranslationTools .NET Client

The .NET client and CLI tool for the TranslationTools service. An application declares its
translatable strings in `.resx` files, pushes them to the service, and reads translated values
back at runtime. This file is the glossary for that domain.

## Language

**Translation key**:
The name a translatable string is known by, unique within its origin, ignoring case: `Action.Edit`
and `action.edit` in one origin are the same key, as the service treats them. Written in `.resx` as the
`data` element's `name`.
_Avoid_: resource name, string id

**Declaration**:
Every translation key in the application's `.resx` files, with every value those files hold, sent under its Environment each time the client initializes. The service makes those keys the Environment's complete set, replacing the previous one, and stores the values by Seeding.
_Avoid_: missing keys, key sync, startup push

**Seeding**:
How the service stores a value from a Declaration: only where it holds no value for that key and locale. A value already on the service stays, whoever wrote it, so a restart never overwrites an edit made in TranslationTools.
_Avoid_: create-only push, fill

**Origin**:
The `.resx` file a translation key came from, written as `<project>:<path>`. Two keys with the
same name in different files are different translations.
_Avoid_: resource set, source, namespace

**Neutral value**:
The value a translation key has in its `.resx` file with no locale suffix. It is the text the
developer wrote, not a translation of anything.
_Avoid_: default value, fallback text, source string

**Default locale**:
The locale the application names as its own. A Declaration sends each Neutral value under it, and
the client puts it in place of any locale it cannot use.
_Avoid_: fallback locale, base locale

**Effective locale**:
The locale a lookup or a Preload actually runs against, after the client has replaced a locale it
cannot use. A locale the client cannot use is one with no name, such as the invariant culture. Its
effective locale is the Default locale.
_Avoid_: resolved culture, target locale

**Preload**:
A fetch of one whole locale when the client initializes, so that later lookups in that locale
answer from the client's cache instead of calling the service. The client preloads the Effective
locale of each locale the application supports, or of the culture it started under when it names
none.
_Avoid_: warm-up, prefetch, locale sync

**Local fallback**:
The value a lookup returns when the service cannot answer it, taken from the `.resx` data the
source generator embedded in the application. A local fallback is real text the application
shipped with, not a placeholder.
_Avoid_: offline value, cached value, degraded value

**Environment**:
A name a deployment reports itself under, such as `production` or `staging`, so the service can
scope which translation keys it serves to that deployment. Blank or whitespace means the unnamed
Environment. A set name may use letters, digits, `.`, `_`, or `-` only, must be at most 64
characters after trim, and must not be `.` or `..`.
_Avoid_: stage, tier, ring

**Global placeholder**:
A `{token}` whose value the client resolves on every render from application state, rather than
from an argument at the call site.
_Avoid_: ambient token, context variable

**Key-scoped token**:
A `{token}` whose value the caller supplies per call, as a parameter on the generated accessor.
_Avoid_: local placeholder, argument token
