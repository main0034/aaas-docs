"""Step 2c scorer: which variants each spec-tester file catches, alone and in pairs.

usage: score2.py <brief> <label>=<file.cs> [<label>=<file.cs> ...]

A variant is a code state that should fail acceptance: a planted bug (mutants.py and
SUMMARY below) or a real defect (a PR head known to be wrong). "Caught" means at
least one test in the file fails on it. Each file is also run on a known-good head;
any failure there is a false red.
"""
import itertools
import json
import os
import re
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(__file__))
from mutants import M  # noqa: E402

DEMO = "/home/claude/aaas-app-demo"
W = "/home/claude/w"
SUMMARY = [
    ("src/App/Program.cs", "today.AddDays(7)", "today.AddDays(6)", "dueSoon ends today+6"),
    ("src/App/Program.cs", "i.DueDate.Value < today)", "i.DueDate.Value <= today)", "overdue includes today"),
    ("src/App/Program.cs", "i.DueDate.Value >= today && ", "", "dueSoon includes overdue"),
    ("src/App/Program.cs", '["2"] = open.Count', '["2"] = rows.Count', "done counted at priority 2"),
    ("src/App/Program.cs", "Open: open.Count", "Open: rows.Count", "open counts done"),
    ("src/App/Program.cs", "q.Length > 100", "q.Length >= 100", "q of 100 refused"),
    ("src/App/Program.cs", "q.Length > 100", "q.Length > 101", "q of 101 accepted"),
    ("src/App/Program.cs", "query = query.WhereSearch(q.Trim());",
     'query = query.Where(i => EF.Functions.ILike(i.Title, "%" + q.Trim() + "%"));', "search title only"),
    ("src/App/Program.cs", '["none"] = open.Count', '["none"] = rows.Count', "done counted under none"),
]
# brief -> (good head, [(label, head, mutant-or-None)])
PLAN = {
    "item-summary": ("0b1fd7d", [(m[3], "0b1fd7d", m) for m in SUMMARY]),
    "projects": ("1f32677", [("REAL #16 trimmed name", "pr16", None)] + [(m[3], "pr16", m) for m in M["projects"][1]]),
    "item-tags": ("pr15", [("REAL #15 /tags 500", "a15cd23", None)] + [(m[3], "pr15", m) for m in M["item-tags"][1]]),
    "item-upcoming": ("pr14", [(m[3], "pr14", m) for m in M["item-upcoming"][1]]),
}
env = dict(os.environ, PATH="/home/claude/.dotnet:" + os.environ["PATH"], DOTNET_CLI_TELEMETRY_OPTOUT="1",
           DOTNET_NOLOGO="1", TEST_POSTGRES="Host=localhost;Username=postgres;Password=x")


def sh(cmd, cwd):
    return subprocess.run(cmd, cwd=cwd, env=env, capture_output=True, text=True, shell=True)


def tree(head):
    path = os.path.join(W, head)
    if not os.path.isdir(path):
        os.makedirs(W, exist_ok=True)
        sh(f"git worktree add -q -f {path} {head}", DEMO)
    return path


def run(head, mutant, files):
    """{label: (failed, total)} for each spec file on this code state."""
    repo = tree(head)
    sh("git checkout -q -- . && git clean -fdq tests/App.Tests/Acceptance", repo)
    if mutant:
        p = os.path.join(repo, mutant[0])
        s = open(p).read()
        i = s.find('app.MapGet("/items/summary"') if "summary" in mutant[3] or mutant in SUMMARY else 0
        j = s.index(mutant[1], max(i, 0))
        open(p, "w").write(s[:j] + mutant[2] + s[j + len(mutant[1]):])
    acc = os.path.join(repo, "tests/App.Tests/Acceptance")
    os.makedirs(acc, exist_ok=True)
    if not os.path.exists(os.path.join(acc, "AcceptanceBase.cs")):
        shutil.copy(os.path.join(os.path.dirname(__file__), "AcceptanceBase.cs"), acc)
    for label, f in files:
        s = open(f).read()
        s = re.sub(r"^namespace [\w.]+;", f"using Acceptance;\nnamespace Score.{label};", s, flags=re.M)
        open(os.path.join(acc, f"Score_{label}.cs"), "w").write(s)
    b = sh("dotnet build tests/App.Tests -c Release -p:TreatWarningsAsErrors=false -p:EnforceCodeStyleInBuild=false -v q", repo)
    if b.returncode:
        raise SystemExit(f"build failed on {head} {mutant and mutant[3]}:\n{b.stdout[-2000:]}")
    out = {}
    for label, _ in files:
        r = sh(f"dotnet test tests/App.Tests -c Release --no-build -- --filter-namespace Score.{label}", repo)
        t = re.search(r"total: (\d+)", r.stdout)
        failed = sorted(x.split(".")[-1] for x in re.findall(r"^failed (\S+)", r.stdout, re.M))
        out[label] = (failed, int(t.group(1)) if t else 0)
    sh("git checkout -q -- . && git clean -fdq tests/App.Tests/Acceptance", repo)
    return out


def main():
    brief = sys.argv[1]
    files = [tuple(a.split("=", 1)) for a in sys.argv[2:]]
    good, variants = PLAN[brief]
    result = {"brief": brief, "files": [l for l, _ in files], "good": run(good, None, files), "variants": {}}
    labels = [l for l, _ in files]
    base: dict[str, dict] = {}
    hit: dict[str, dict[str, bool]] = {}
    for label, head, mutant in variants:
        r = run(head, mutant, files)
        if mutant and head not in base:
            base[head] = run(head, None, files)
        before = base.get(head) if mutant else None
        # Caught: a test fails that did not already fail on the unmutated head.
        hit[label] = {l: bool(set(r[l][0]) - set(before[l][0] if before else [])) for l in labels}
        result["variants"][label] = r
        print(label, {l: (hit[label][l], len(r[l][0])) for l in labels}, flush=True)
    result["hit"] = hit
    caught = {l: sum(1 for v in hit.values() if v[l]) for l in labels}
    pairs = {f"{a}+{b}": sum(1 for v in hit.values() if v[a] or v[b])
             for a, b in itertools.combinations(labels, 2)}
    result["caught"], result["pairs"], result["n"] = caught, pairs, len(variants)
    print(json.dumps({"false reds on good head": {l: result["good"][l][0] for l in labels}, "caught": caught,
                      "pairs": pairs, "of": len(variants)}, indent=1))
    json.dump(result, open(f"/home/claude/score/{brief}.json", "w"), indent=1)


main()
