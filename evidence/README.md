# Evidence

The raw material behind findings 21-29: what the agents were asked, and the tests that judged them. Kept here so
`aaas-agent` holds nothing about one application (D-25, finding 29), and so a finding's numbers can be re-run.

| Path | What | Findings |
|---|---|---|
| `briefs/` | Every request given to the harness before requests moved into the app repo (`requests/`, `changes/`) | 21-28 |
| `acceptance/operator/` | The operator's hidden acceptance tests, written from each brief before its run, and `run-acceptance.sh` | 26 |
| `acceptance/spec-tester/2026-10-04/` | Spec-tester files from step 2a | 27 |
| `acceptance/spec-tester/2026-10-10/` | Spec-tester files from steps 2b-2c: `-p` ran inside `create-change`, `-a` / `-b` are two independent runs on the same brief | 28, 29 |
| `acceptance/score/` | `mutants.py` (planted bugs), `score.py` (step 2a), `score2.py` (step 2c: alone and in pairs), `results/` | 27-29 |

`score2.py` expects `aaas-app-demo` cloned at `/home/claude/aaas-app-demo` with the PR heads fetched
(`pull/14/head:pr14` and so on), .NET 10 and a Postgres 16 on localhost; see `STATUS.md`, "Things to remember".
