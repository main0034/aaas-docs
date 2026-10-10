In the items app (the repository `aaas-app-demo`): we want to see what is coming up.

- A list of open items that are due soon: from today up to and including a number
  of days ahead. Today counts; so does the last day. Items already overdue are not
  in this list, and neither are items without a due date or done items.
- If no number of days is given, use 7.
- The number of days must be between 0 and 365. Anything else is refused.
- Soonest due first. Items due on the same day: the one added first comes first.

Interface (fixed - build exactly this):

- `GET /items/upcoming?days=N` returns `200` with a JSON array of items, in the same
  shape `GET /items` returns them.
- `days` outside 0-365 (or not a number) returns `400`.
- "Today" is the current UTC date.

There is nobody to ask follow-up questions. Say in the pull request what you
built and how you know it works.
