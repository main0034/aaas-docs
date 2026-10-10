# usage: score.py <brief> <spec-file.cs> [<spec-file2.cs> ...]
# Runs operator (namespace Acceptance) and each spec-tester file (namespace Spec<N>) against
# the PR head and each planted bug. Prints a table: rows = variants, cols = suites (pass/total).
import os, re, subprocess, sys, shutil
sys.path.insert(0, os.path.dirname(__file__)); from mutants import M
brief, specs = sys.argv[1], sys.argv[2:]
repo, muts = M[brief]
acc = os.path.join(repo, "tests/App.Tests/Acceptance")
env = dict(os.environ, PATH="/home/claude/.dotnet:" + os.environ["PATH"], DOTNET_CLI_TELEMETRY_OPTOUT="1",
           TEST_POSTGRES="Host=localhost;Username=postgres;Password=x")
def sh(cmd): return subprocess.run(cmd, cwd=repo, env=env, capture_output=True, text=True, shell=True)
sh("git checkout -q -- src"); shutil.rmtree(acc, ignore_errors=True); os.makedirs(acc)
shutil.copy("/home/claude/acceptance/AcceptanceBase.cs", acc)
shutil.copy(f"/home/claude/acceptance/{brief}.acceptance.cs", acc)
suites = ["Acceptance"]
for i, f in enumerate(specs):
    ns = f"Spec{i+1}"; s = open(f).read()
    s = re.sub(r"^namespace Acceptance;", f"using Acceptance;\nnamespace {ns};", s, flags=re.M)
    open(os.path.join(acc, f"{ns}.cs"), "w").write(s); suites.append(ns)
def run():
    b = sh("dotnet build tests/App.Tests -p:TreatWarningsAsErrors=false -p:EnforceCodeStyleInBuild=false -v q")
    if b.returncode: return {s: "BUILD FAIL" for s in suites}, b.stdout[-1500:]
    out, detail = {}, ""
    for s in suites:
        r = sh(f"dotnet test tests/App.Tests --no-build -- --filter-namespace {s}")
        t = re.search(r"total: (\d+)", r.stdout); ok = re.search(r"succeeded: (\d+)", r.stdout)
        out[s] = f"{ok.group(1) if ok else '?'}/{t.group(1) if t else '?'}"
        fails = re.findall(r"^failed (\S+)", r.stdout, re.M)
        if fails: detail += f"  {s} failed: " + ", ".join(x.split('.')[-1] for x in fails) + "\n"
    return out, detail
rows = [("PR head", None)] + ([] if os.environ.get("HEAD_ONLY") else [(m[3], m) for m in muts])
print("| variant | " + " | ".join(suites) + " |"); print("|---" * (len(suites)+1) + "|")
details = ""
for label, m in rows:
    sh("git checkout -q -- src")
    if m:
        p = os.path.join(repo, m[0]); s = open(p).read(); assert s.count(m[1]) == 1, (label, s.count(m[1]))
        open(p, "w").write(s.replace(m[1], m[2]))
    out, d = run()
    print(f"| {label} | " + " | ".join(out[s] for s in suites) + " |", flush=True)
    if d: details += f"{label}:\n{d}"
sh("git checkout -q -- src"); shutil.rmtree(acc, ignore_errors=True)
print("\n" + details)
