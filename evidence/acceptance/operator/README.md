# Hidden acceptance tests (Phase 7)

Written from each brief **before** its run, never committed to an app repo, never shown to the agent.
They are black-box: rows are created and read only through HTTP, using the interface section of the
brief, so they do not depend on how the agent modelled anything.

To score a PR: clone the app repo, check out the PR head, then

    ./run-acceptance.sh <checkout> <brief-name>     # e.g. item-tags

which copies `AcceptanceBase.cs` and `<brief>.acceptance.cs` into `tests/App.Tests/Acceptance/` and runs
only that namespace against `TEST_POSTGRES` (Postgres 16, .NET 10). Style analyzers are off for these
files; the agent's own code is still held to them by CI. Paths in the script assume Claude's container.

Results: FINDINGS.md finding 26.
