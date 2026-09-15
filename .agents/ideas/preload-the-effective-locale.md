# Preload the effective locale

Status: needs-triage

## Motivation

The client can fetch whole locales at startup, so that later lookups answer from the cache instead
of calling the service. An application chooses which locales to preload. An application that chooses
none falls back to the culture of the thread that started it, and a culture the client cannot use is
then dropped from that list.

Those two rules combine badly for one kind of application. A worker or console application that runs
under the invariant culture configures no locales, offers a culture that gets dropped, and so
preloads nothing. Every lookup it makes is a live call.

This is the same kind of application the lookup failure contract was written for. It gets a
warm-cache feature that never warms.

The size of the prize is easy to state. A nightly job that reads 38 translations makes 38 requests
per run today. Preloading the locale it will actually use replaces those with one request at startup.

The size of the prize is also easy to overstate. Nobody has reported those requests as a problem, and
the failure contract already makes them safe. This is a saving, not a fix.

## Goal

A worker application that reads translations starts with the locale it will actually use already
loaded, without configuring anything.

## Decisions (locked)

- A lookup replaces a locale it cannot use with the configured default locale. That is settled
  elsewhere and this idea does not reopen it.
- A lookup that misses the cache is safe: it degrades to the application's own text rather than
  failing. So preloading is a saving and never a correctness fix. If this idea is dropped, nothing
  breaks.
- Preloading still skips a locale it cannot use. This idea is about what counts as unusable, not
  about whether the skip exists.
- A whole-locale fetch still fails loudly rather than degrading, and startup still catches and logs
  that failure. Preloading more locales must not turn a service outage into a failed start.

## Out of scope

- The lookup failure contract itself — what degrades, what throws, timeouts, and the suppression
  window after a failure. Decided already.
- Resolving a regional locale to its parent, such as Belgian Dutch to Dutch.
- Cache expiry, and refreshing a preloaded locale in the background.
- Changing how an application declares which locales it supports.
- Preloading anything other than a whole locale.

## Open questions

1. Should an application that configures no locales preload at all? Making it preload the default
   locale means every application pays a startup request, including the many that never read a
   translation.
2. When the starting thread's culture is meaningful, such as in a web application that configured
   nothing, should the default locale be preloaded as well as that culture, or instead of it?
3. Should the same substitution apply to locales an application configured explicitly? An
   application can name an unusable culture on purpose. It is unclear whether that is a mistake to
   reject when the client is built, or a request for the default locale.
4. Is this behaviour, or an option? An option is safer and adds a thing to explain.
5. How would anyone know it helped? The claim is fewer requests, and nobody is currently counting
   them. Without a way to see the difference, this stays speculative.
6. Does a short-lived process benefit at all? It pays one whole-locale fetch to avoid several
   single-key fetches. A job that reads two translations is worse off, and a job that reads forty is
   better off. The break-even point is unknown.
