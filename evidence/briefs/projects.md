In the items app (the repository `aaas-app-demo`): we want to group items into
projects and see how far along each project is.

- A project has a name. Two projects cannot have the same name, ignoring letter
  case. A name is 1 to 100 characters after trimming, and is stored trimmed.
- An item belongs to at most one project. It can be moved to another project or
  taken out of its project.
- For each project we want: how many items it has, how many of them are done, and
  the percentage done as a whole number, rounded down. A project with no items is
  0 percent done.
- A project's page lists its items: open items first, then done items; within
  each group, the item added most recently comes first.
- A project can be deleted only when it has no items.
- Existing items and their data must survive the change.

Interface (fixed - build exactly this):

- `POST /projects` with body `{"name": "Garden"}` returns `201` with
  `{"id": 1, "name": "Garden"}`. Invalid name: `400`. Name taken: `409`.
- `PUT /items/{id}/project` with body `{"projectId": 1}` (or `null` to take the item
  out) returns `200` with the item. Unknown item: `404`. Unknown project: `400`.
- `GET /projects` returns `200` with
  `[{"id": 1, "name": "Garden", "total": 4, "done": 1, "percentDone": 25}, ...]`,
  sorted by name, ignoring letter case.
- `GET /projects/{id}` returns `200` with the same fields plus `"items": [...]`, the
  items in the shape `GET /items` returns them, in the order described above.
  Unknown project: `404`.
- `DELETE /projects/{id}` returns `204`; `409` if it still has items; `404` if unknown.

There is nobody to ask follow-up questions. Say in the pull request what you
built and how you know it works.
