In the items app (the repository `aaas-app-demo`): we want to give items a due
date, and see at a glance what is late.

- An item can have a due date (a day, no time). It is optional, can be set when
  the item is created, and can be changed or cleared later.
- We want a list of overdue items: items that are not done and whose due date is
  before today. The most overdue comes first. Done items are never overdue, and
  items without a due date are never overdue.
- Items we already have must keep working as they are, with no due date.

The tests must go through the endpoints themselves and show which items come back
in the overdue list - including an item due today, a done item that is past its
due date, and an item with no due date, none of which should be listed.

There is nobody to ask follow-up questions. Say in the pull request what you
built and how the tests show it works.
