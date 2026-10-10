A fix to an open pull request, not a new feature. In `aaas-app-demo`, check out
the existing branch `feat/notebook` (PR #6), fix the bug below, and push to the
**same branch**. Do not create a new branch or a new pull request. Add a comment
to PR #6 (`gh pr comment 6 --body-file ...`) saying what was wrong and what you
changed. PR #6 must still never be merged.

## The bug

When `Assistant:Enabled=true` and the `claude` program does not exist at
`Assistant:ClaudePath`, typing a question returns HTTP 500 and the question is
saved with neither an answer nor an error. Reproduced against a real Postgres:

```
POST /notebook/lines {"text":"Is the CLI missing?", ...}  -> 500
System.ComponentModel.Win32Exception (2): An error occurred trying to start
process '/nonexistent/claude' ... No such file or directory
```

`ClaudeAssistant` catches timeouts but not a failure to start the process, so
the exception escapes the endpoint after the question row was already saved.

## What should happen

The brief that produced this feature said: when the CLI is missing, the
question is still saved and the page shows a short reason where the answer
would be, and nothing returns a stack trace. So: a failure to start the process
(and any other failure while talking to it) becomes an `AssistantResult` with a
readable error - for a missing program, one that names the configured path -
the question gets that error and `AnsweredAt`, and the route returns 201 as it
does for any other assistant failure.

## Test

Add a test that fails on the current code: `ClaudeAssistant` with
`Assistant:ClaudePath` set to a path that does not exist returns an error result
rather than throwing. A path that does not exist starts no process, so this is
within the rule that no test may start a real process.

Why the existing tests missed it: they cover argument building, JSON parsing
and the route with a fake `IAssistant`, and nothing covers the real class's
failure paths. Say that in the PR comment in one sentence.
