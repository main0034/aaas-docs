In the items app (the repository `aaas-app-demo`), we want a **notebook**: a
blank sheet in the browser that you write on with the keyboard and draw on with
the mouse, and that can pass questions to an AI. This is a local-only
experiment. It will run on one laptop and will never be deployed, so **the pull
request must never be merged** - say so at the top of its body. Use the branch
`feat/notebook`.

There is nobody to ask follow-up questions. Make sensible choices within what
is written here and say in the pull request what you assumed.

## What the user does

1. Opens `/` and sees a blank sheet, with a panel beside it listing every item
   on the sheet and whether it is done.
2. Clicks anywhere on the sheet and types. Enter ends the line. Each line is
   saved where it was typed.
   - A line whose trimmed text ends with `?` is a **question**. It is sent to
     the AI and the answer appears on the sheet under the question.
   - Any other non-empty line is an **item**. Items are the existing `Item`
     resource: a typed line becomes an item whose title is the text. Titles are
     already unique; typing a duplicate must show a readable message on the
     page, not fail silently.
3. Draws with the mouse. Every stroke is saved and redrawn on reload.
   - If a stroke is a **check mark** and it is **next to an item** (just left or
     just right of that item's text, on the same line), that item becomes done -
     exactly as `PATCH /items/{id}` with `done: true` does today, `DoneAt`
     included. The panel updates.
   - Any other stroke is just a drawing.
4. Can still tick an item done or not done from the panel (existing PATCH).

Everything - items with their positions, strokes, questions and answers -
survives a reload. Items that existed before this change have no position;
they appear in the panel only.

## Where the logic lives

CI tests C# only. JavaScript here has no test runner, so **the browser does no
deciding**. It captures keystrokes and mouse points, measures the rendered size
of each typed line, sends them to the server and draws what comes back. The
decisions are made in C# and unit-tested without a database, per `AGENT.md`:

- **Is this line a question?** A pure function. `"?"` on its own is not a
  question and is not an item either (400).
- **Is this stroke a check mark?** Deterministic geometry, no ML and no
  third-party recogniser. A check is one stroke that goes down-and-right to its
  lowest point and then up-and-right, with the lowest point not at either end,
  both parts of a reasonable length, and a bounding box in a sane size range
  (say 10-200 px). A straight line, a dot, a circle or a scribble is not a
  check. Test each of those, and several hand-drawn-looking checks with jitter.
- **Which item is it next to?** Store each item's position and measured
  width/height when it is typed. A check belongs to the nearest item whose line
  its vertical centre falls within, if its box ends within ~100 px left of the
  text or starts within ~100 px right of it. People draw over the edge, so a
  check may also overlap the text's first or last ~15 px. Otherwise it belongs
  to no item.
  Test left, right, too far, wrong line, and two candidate items.

The browser side is one static page under `src/App/wwwroot` (HTML, CSS and
plain JavaScript, no framework, no npm, no build step), served by the app.

## The AI: the local Claude Code CLI

Questions go to the `claude` command-line program installed on the laptop the
app runs on, using that person's own Claude login. No API key and no SDK
package, so no secret enters the application. Behind an interface (for example
`IAssistant`) so another local AI can be added later.

Run it exactly like this, with no shell in between (`ProcessStartInfo` with
`ArgumentList`, never a command string):

```
claude -p --output-format json --tools "" --strict-mcp-config --no-session-persistence
```

- The question is written to the process's **stdin**, never passed as an
  argument - a question starting with `--` must not become a flag.
- `--tools ""` gives the model no tools at all: a question typed on the sheet
  can make it answer, never act.
- Working directory: a fresh empty temporary directory, not the app's.
- stdout is JSON; the answer is the `result` string, and `is_error: true` means
  it failed. On timeout (default 120 s) **kill the process tree**
  (`Kill(entireProcessTree: true)`) - cancelling the wait alone leaves `claude`
  running. Read stdout and stderr concurrently; an unread stderr pipe that
  fills up stalls the child until the timeout.
- Settings, through `IConfiguration`: `Assistant:Enabled` (default **false**),
  `Assistant:ClaudePath` (default `claude`), `Assistant:TimeoutSeconds`.

When the assistant is disabled, the CLI is missing, it times out or it
reports an error, the question is still saved and the page shows a short
reason where the answer would be. Nothing returns a stack trace. Test the
argument list, the JSON parsing (success, `is_error`, malformed output) and the
question route with a fake `IAssistant`; no test may start a real process.

## Running it on the laptop

Add a "Run the notebook locally" section to `README.md` with the exact commands
for macOS: Postgres in Docker, `migrate`, then the app with
`Assistant__Enabled=true`, listening on `localhost` only - anyone who can reach
the port can spend the owner's Claude usage. Add a way to set the listen
address (for example `BIND_ADDRESS`, default unchanged `0.0.0.0`, because the
container needs that). The app must run with `dotnet run` on the host rather
than in the container, because the `claude` CLI and its login live on the
host; say so in the README. Do not touch the `Dockerfile`.

## Data model

Expand-only migrations, as `AGENT.md` requires. Nullable position and size
columns on `items`; new tables for strokes (points as `jsonb` is fine) and
questions (text, position, answer, error, asked-at, answered-at).

## Budget

This is a larger change than earlier ones; you have up to 150 turns. Do not
spend them re-reading files you have already read. To make a helper visible to
the tests, use `InternalsVisibleTo` or make it `public` - decide once.
