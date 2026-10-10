In the items app (the repository `aaas-app-demo`): we want one place that says where
things stand.

- **Open** items are the ones not marked done. Count them.
- Of the open items: how many are **overdue** (due date before today) and how many
  are **due soon** (due today or on one of the next 7 days, so the last day counted
  is today + 7). An overdue item is not also due soon. Open items without a due date
  are neither. "Today" is the current date in UTC.
- Of the open items: how many there are at each priority, 1 to 5, and how many have
  no priority. Every priority appears in the answer, with 0 when there are none.
- **Done** items: count them.
- It must be possible to narrow everything above to the items whose title or note
  contains a search term, ignoring letter case - the same matching `GET /items?q=`
  already does. A term that is empty or only spaces means no narrowing. A term
  longer than 100 characters is refused.
- Nothing is stored or changed by asking for the summary.

Interface (fixed - build exactly this):

- `GET /items/summary` and `GET /items/summary?q=term` return `200` with
  `{"open": 5, "overdue": 1, "dueSoon": 2, "done": 3,
    "byPriority": {"1": 0, "2": 1, "3": 0, "4": 0, "5": 0, "none": 4}}`.
  A `q` longer than 100 characters: `400`.

There is nobody to ask follow-up questions. Say in the pull request what you
built and how you know it works.
