An addition to an open pull request. In `aaas-app-demo`, check out the existing
branch `feat/notebook` (PR #6), build on it, and push to the **same branch**. No
new branch or pull request. When done, comment on PR #6 (`gh pr comment 6
--body-file ...`) for the person who uses the notebook. Never merge PR #6.

Read the handwriting code already on the branch first (pending ink, Enter to
read, `IHandwritingReader`); this builds on it and changes none of its rules.

## First: fix a bug in what counts as pending ink

Reproduced in a browser against a real Postgres and the real CLI. After a page
reload, every stroke with `read_at` null is shown blue and treated as pending -
including strokes from earlier sessions, and check marks that already ticked an
item. The next Enter sends them all: writing "Buy bread" produced an item whose
box was 266x420 px, spanning old check marks near the top of the sheet, so the
grey recognised text and the check-mark target were far from the ink.

The rules, both enforced where they decide something:

- **A stroke that ticked an item is never pending and can never be read.** Record
  it on the server when the stroke route ticks the item (for example set
  `read_at`, or a nullable column naming the ticked item - your choice, expand
  only), so the read route rejects it like an already-read stroke, and the
  browser never marks it pending.
- **Only strokes drawn in the current page session are pending.** Unread strokes
  loaded from the server on page load are ordinary drawings in black; they are
  not pending and are not sent by Enter or by a preview. This is a browser rule
  (what it offers to read); the server's all-or-nothing validation stays.

Test the server half: a stroke that ticked an item is rejected by the read route.

## What the user wants

While writing by hand, to see what the notebook thinks they wrote, without
pressing anything - and without anything being created until they press Enter.

## Behaviour

1. When there is pending ink and the user has not drawn for **2 seconds**, the
   browser renders the pending ink exactly as it does for Enter and posts it to
   a new **preview** route.
2. The server reads it with the same `IHandwritingReader` and returns only the
   text. It creates nothing, marks no stroke as read, and never calls the AI
   assistant.
3. The page shows the text in grey italics under the pending ink (and "reading..."
   while a preview is in flight). A preview that returns after the ink has
   changed is discarded; at most one preview is in flight, and if the ink changed
   meanwhile, one more is scheduled when it returns.
4. Nothing legible: show nothing (no error message for previews). Reader failure:
   show a short grey note, no pop-up.
5. A **Live preview** checkbox in the panel, on by default, turns this off.
6. Enter, Escape and check marks behave exactly as today.

## Do not read the same ink twice

Keep the last few preview results in memory on the server (a small bounded
cache, e.g. 32 entries, 10 minutes), keyed by the SHA-256 of the PNG bytes.
When Enter posts ink whose PNG hash is cached, the read route uses the cached
text instead of calling the reader again. The decision is the server's, from the
bytes it received - the browser does not send the previewed text. A cached
"nothing legible" is reused too. Register the cache as a singleton.

## Tests (no process, no database)

- the preview route with a fake reader: returns text, creates no item or
  question, marks no stroke read, does not call the fake assistant
- preview then read with identical PNG bytes: the fake reader is called once
- different bytes: called twice
- cache bound and expiry (inject the clock, e.g. `TimeProvider`)
- the same PNG and size validation as the read route

## Budget

Up to 120 turns. Do not re-read files you have already read.
