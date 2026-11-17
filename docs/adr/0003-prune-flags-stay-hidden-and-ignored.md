---
status: accepted
---

# The prune flags stay hidden and ignored

`translations push --prune` and `translations pull --prune` both do nothing. The service has ignored the push flag since 2026-06-27, and the pull flag never deleted anything. We keep both options, hidden from `--help`, accepted, and ignored without a message. Scripts outside this repo pass them, and System.CommandLine 2.0.3 fails a command with an unknown option and exit code 1. Removing either option would break those scripts and force a MAJOR bump of every package for a flag that has no effect. No later change removes them.

## Considered options

- **Remove both options.** Rejected: it breaks every script that passes `--prune` and needs a MAJOR version, for no gain to anyone running the tool.
- **Keep them visible with a "does nothing" note, or print a warning when they are passed.** Rejected: `--help` would list options nobody should use, and a warning on every CI run is noise the developer cannot act on.
- **Make `pull --prune` delete a locale `.resx` file the service holds nothing for.** Rejected: that file only goes stale when someone removes every value in one locale for an Origin on the service, which nobody has reported, and deleting files in a developer's checkout is a bigger promise than that gap.
