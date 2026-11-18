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
Every translation key in the application's `.resx` files, with every value those files hold, sent under its Environment each time the client initializes. The service adds those keys to the Environment, stamps every key the Declaration names, and removes none; a key expires after 7 days of Declarations without it. It stores the values by Seeding.
_Avoid_: missing keys, key sync, startup push

**Declare**:
A Declaration the app sends at any time after the client is built, for keys it chooses and under any Origin, with `DeclareAsync`. It is the startup Declaration's request, so it never overwrites a stored value and renews each key's expiry. It never enters the catalog. Unlike the startup Declaration, it throws on failure.
_Avoid_: register, runtime catalog

**Push**:
An upload of the Translation keys and values in the project's `.resx` files to the service, under the project's Environment, with `translations push`. The service adds those keys to the Environment, overwrites each value that differs from the stored one, and removes no key. A key deleted from the `.resx` files leaves the Environment once 7 days of Declarations pass without it, and the service deletes it 24 hours after no Environment holds it.
_Avoid_: sync, upload, prune

**Pull**:
A download of the values the service holds for the project's Origins into the project's locale `.resx` files, with `translations pull`. Each locale file the service holds values for is rewritten to hold exactly those values, so an entry the service no longer holds disappears from it. A pull never writes the file that holds the Neutral values and never deletes a file.
_Avoid_: sync, download, prune

**Write**:
A Push from an app, with `WriteAsync`: values per Origin, key, and locale. The service overwrites each value that differs, keeps the previous text in history, queues Auto-translation, opens Review requests, and declares a key that is new to the Environment. A null value is rejected before any request; an empty string clears the text. After the service accepts it, the client's cache answers the written values at once. It throws on failure.
_Avoid_: update, save, upload

**Read**:
The text the service stores for one key and locale, with `GetStoredValueAsync`, for an app that fills an edit form. It asks the service on every call and bypasses the cache, the local fallback, and the suppression window. Null means nothing is stored (no row, or a null, empty, or whitespace value). It always throws when the service cannot answer, whatever `ThrowOnLookupError` says, so a failed read never looks like an empty field.
_Avoid_: fetch, lookup, get

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

**Keyless mode**:
The state of a client built with `AllowMissingApiKey` set and a blank API key. Every single-key
lookup answers with the Local fallback and nothing is ever sent to the service. A call that needs
the service throws. A present API key ends it: the option changes nothing then.
_Avoid_: offline mode, no-key mode, demo mode

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

**Plural**:
A translation value that holds one whole sentence per Plural form and picks one by a whole number the caller supplies, such as "1 item" or "5 items". It is one ICU plural argument that wraps the entire value: `{count, plural, one {# item} other {# items}}`, where `#` prints the number. A key is a Plural when its Neutral value is one. A Plural is never a fragment inside other text, never nested, and never driven by a Global placeholder. Key-scoped tokens may appear inside its forms.
_Avoid_: plural block, ICU message, quantity string

**Plural form**:
The sentence of a Plural for one CLDR plural category of a locale (`zero`, `one`, `two`, `few`, `many`, `other`), or for an exact number the value names, such as `=0`. An exact number wins over a category.
_Avoid_: variant, plural case, quantity
