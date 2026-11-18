---
status: accepted
---

# Code carries no comments

The build fails on a comment in C# (`MVDM0053`). Code explains itself through names and structure. A reason a name cannot carry goes into an ADR, and the history of a line lives in git. A comment drifts from the code it describes, and no build or test notices when it does. The rule is the one mvdmio-suite settled in its #1433, and this repo runs a copy of the C# part of it.

A comment survives only where something outside the reader needs it:

- A `///` doc comment on a public or protected member of a project that sets `GenerateDocumentationFile`. Only `mvdmio.TranslationTools.Client` sets it, because its documentation ships in the package and its users read it in IntelliSense. The `translations` tool ships as a dotnet tool, so nobody references its types, and the source generator ships only as a DLL inside the Client package. Neither sets the property.
- The reason at the end of a `#pragma warning disable` line. A justified exception still needs its reason, and it sits on the one line it excuses. A reason on the line above the pragma fails.
- A comment a tool reads as an instruction. The copied rule allows the suite's `pre-review:` waiver, which no tool in this repo reads.

The rule lives in `mvdmio.TranslationTools.Lint`, an analyzer project that is never packed. Every project in the repo references it, test projects included.

## Considered options

- **A copy in a project that is never packed (chosen).** About 190 lines of the suite's analyzer, copied. It needs no package and no release. The cost is that the copy can drift from the suite's rule, so a change to the rule is made in each repo that holds a copy.
- **Add the rule to the source generator.** Rejected. The generator ships inside the Client package and runs in every consumer's build, so it would fail other people's builds on their own comments.
- **Publish the suite's rule as its own package and reference it here.** Rejected for now. It needs a new package, a publish workflow, and its own versioning, to share about 190 lines that rarely change.

## Consequences

- The change that adds the rule deletes about 440 comment lines. To find a deleted reason, run `git log -S` with its text, or `git blame` the file at the commit before that change.
- Comments in `.csproj` files, `.editorconfig`, and the workflow YAML do not reach the C# compiler and stay.
- Nothing checks that the rule stays out of the Client package. The package copies only the generator DLL, by explicit path, and the lint reference is private to each project. A consumer whose build broke on their own comments would be the first to notice.
