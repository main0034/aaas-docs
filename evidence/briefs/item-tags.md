In the items app (the repository `aaas-app-demo`): we want to tag items, for example
"home", "work", "urgent", and filter by tag.

- An item has zero or more tags. Setting an item's tags replaces the tags it had.
- Tags are not case sensitive and surrounding spaces do not count: " Home" and
  "home" are the same tag, and it is stored and shown as "home". Giving the same
  tag twice keeps it once.
- A tag is 1 to 30 characters after trimming. An item has at most 10 tags.
  Anything else is refused, and the item keeps the tags it had.
- The existing item list can be filtered by tag. The filter works together with
  the filters the list already has (open items only, search).
- We want a list of all tags in use, with how many items carry each one.
  A tag no item carries any more is not in that list.
- Existing items and their data must survive the change.

Interface (fixed - build exactly this):

- `PUT /items/{id}/tags` with body `{"tags": ["home", "urgent"]}` returns `200` with
  the item's tags as a JSON array of strings, sorted alphabetically. Unknown item:
  `404`. Invalid tags: `400`. `{"tags": []}` removes all tags.
- `GET /items/{id}/tags` returns `200` with the same sorted array. Unknown item: `404`.
- `GET /items?tag=home` returns only items carrying that tag (any letter case in
  the query), combinable with `open=true` and `q=...`, ordered as `/items` already is.
- `GET /tags` returns `200` with `[{"name": "home", "items": 3}, ...]`, sorted by name.
  `items` counts every item with the tag, done or not.

There is nobody to ask follow-up questions. Say in the pull request what you
built and how you know it works.
