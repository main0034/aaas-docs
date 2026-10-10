# Planted bugs, one line each, applied to a PR head. (dir, file, old, new, label)
M = {
 "item-priority": ("/home/claude/aaas-app-demo", [
   ("src/App/Program.cs", "IQueryable<Item> query = ctx.Items.AsNoTracking().WhereOpen();\n    if (maxPriority", "IQueryable<Item> query = ctx.Items.AsNoTracking();\n    if (maxPriority", "done items listed"),
   ("src/App/Data/ItemQueryExtensions.cs", ".ThenByDescending(i => i.Id)", ".ThenBy(i => i.Id)", "ties oldest first"),
   ("src/App/Data/ItemQueryExtensions.cs", "i.Priority <= maxPriority", "i.Priority < maxPriority", "cutoff exclusive"),
 ]),
 "item-upcoming": ("/home/claude/pr14", [
   ("src/App/Data/ItemQueryExtensions.cs", "i.DueDate <= to", "i.DueDate < to", "window end exclusive"),
   ("src/App/Data/ItemQueryExtensions.cs", "i.DueDate >= from && ", "", "overdue included"),
   ("src/App/Program.cs", "int d = 7;", "int d = 3;", "default 3 days"),
   ("src/App/Program.cs", ".OrderBy(i => i.DueDate)\n        .ThenBy(i => i.Id)", ".OrderBy(i => i.DueDate)\n        .ThenByDescending(i => i.Id)", "same-day newest first"),
 ]),
 "item-tags": ("/home/claude/pr15", [
   ("src/App/Program.cs", "query.WhereTag(tag.Trim().ToLowerInvariant())", "query.WhereTag(tag.Trim())", "tag filter case-sensitive"),
   ("src/App/Endpoints/TagEndpoints.cs", "normalized.Count > 10", "normalized.Count > 11", "11 tags allowed"),
   ("src/App/Endpoints/TagEndpoints.cs", "t.Length > 30", "t.Length > 31", "31-char tag allowed"),
   ("src/App/Endpoints/TagEndpoints.cs", ".OrderBy(r => r.Name)", ".OrderByDescending(r => r.Name)", "/tags reverse-sorted"),
 ]),
 "projects": ("/home/claude/pr16", [
   ("src/App/Endpoints/ProjectEndpoints.cs", "done * 100 / total", "(done * 100 + total - 1) / total", "percent rounds up"),
   ("src/App/Endpoints/ProjectEndpoints.cs", ".OrderBy(i => i.IsDone)", ".OrderByDescending(i => i.IsDone)", "done items first"),
   ("src/App/Endpoints/ProjectEndpoints.cs", "i.ProjectId == id, ct", "i.ProjectId == id && !i.IsDone, ct", "delete allowed with only done items"),
 ]),
}
