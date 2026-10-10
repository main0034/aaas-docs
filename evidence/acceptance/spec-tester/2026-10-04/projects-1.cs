using System.Net;
using System.Text.Json;

namespace Acceptance;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    // ── POST /projects ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        var p = await PostAsync("/projects", new { name = "Garden" });
        Assert.True(p.TryGetProperty("id", out var idEl) && idEl.GetInt32() > 0);
        Assert.Equal("Garden", p.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_stores_name_trimmed()
    {
        var p = await PostAsync("/projects", new { name = "  Garden  " });
        Assert.Equal("Garden", p.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_name_1_char_is_accepted()
    {
        await PostAsync("/projects", new { name = "A" });
    }

    [Fact]
    public async Task Create_project_name_100_chars_is_accepted()
    {
        await PostAsync("/projects", new { name = new string('A', 100) });
    }

    [Fact]
    public async Task Create_project_name_101_chars_is_rejected()
    {
        await PostAsync("/projects", new { name = new string('A', 101) }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_empty_name_is_rejected()
    {
        await PostAsync("/projects", new { name = "" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_whitespace_only_name_is_rejected()
    {
        await PostAsync("/projects", new { name = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_name_100_chars_after_trim_is_accepted()
    {
        // Raw length 102, trimmed length 100: validation must use the trimmed value
        await PostAsync("/projects", new { name = " " + new string('B', 100) + " " });
    }

    [Fact]
    public async Task Create_project_name_101_chars_after_trim_is_rejected()
    {
        // Raw length 103, trimmed length 101: still invalid after trimming
        await PostAsync("/projects", new { name = " " + new string('C', 101) + " " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_duplicate_name_exact_is_rejected()
    {
        await PostAsync("/projects", new { name = "Garden" });
        await PostAsync("/projects", new { name = "Garden" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_duplicate_name_different_case_is_rejected()
    {
        await PostAsync("/projects", new { name = "Garden" });
        await PostAsync("/projects", new { name = "GARDEN" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_duplicate_name_after_trim_and_case_fold_is_rejected()
    {
        await PostAsync("/projects", new { name = "Garden" });
        // "  garden  " trims to "garden", which is case-insensitively equal to "Garden"
        await PostAsync("/projects", new { name = "  garden  " }, HttpStatusCode.Conflict);
    }

    // ── PUT /items/{id}/project ─────────────────────────────────────────────

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        var item = await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        Assert.Equal(iId, item.GetProperty("id").GetInt32());
        Assert.Equal("Plant roses", item.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Remove_item_from_project_returns_200_with_item()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        var item = await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = (int?)null }, HttpStatusCode.OK);
        Assert.Equal(iId, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Remove_item_from_project_clears_its_membership()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = (int?)null }, HttpStatusCode.OK);
        var project = await GetAsync($"/projects/{pId}");
        Assert.Equal([], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Move_item_to_another_project_updates_counts()
    {
        var p1 = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var p2 = (await PostAsync("/projects", new { name = "Kitchen" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Paint fence");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = p1 }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = p2 }, HttpStatusCode.OK);
        var list = await GetAsync("/projects");
        var proj1 = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == p1);
        var proj2 = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == p2);
        Assert.Equal(0, proj1.GetProperty("total").GetInt32());
        Assert.Equal(1, proj2.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Assign_unknown_item_to_project_returns_404()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        await SendAsync(HttpMethod.Put, "/items/99999/project", new { projectId = pId }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_item_to_unknown_project_returns_400()
    {
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = 99999 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assign_item_to_unknown_project_does_not_change_existing_assignment()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = 99999 }, HttpStatusCode.BadRequest);
        var project = await GetAsync($"/projects/{pId}");
        Assert.Equal(["Plant roses"], Titles(project.GetProperty("items")));
    }

    // ── GET /projects ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_projects_returns_total_done_and_percentDone_rounded_down()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var i1 = await CreateItemAsync("Task A");
        var i2 = await CreateItemAsync("Task B");
        var i3 = await CreateItemAsync("Task C");
        await SendAsync(HttpMethod.Put, $"/items/{i1}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{i2}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{i3}/project", new { projectId = pId }, HttpStatusCode.OK);
        await MarkDoneAsync(i1);
        var list = await GetAsync("/projects");
        var p = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == pId);
        Assert.Equal(3, p.GetProperty("total").GetInt32());
        Assert.Equal(1, p.GetProperty("done").GetInt32());
        // 1/3 * 100 = 33.33…; floor = 33 (not 34)
        Assert.Equal(33, p.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task List_projects_with_no_items_shows_0_percent_done()
    {
        await PostAsync("/projects", new { name = "Empty" });
        var list = await GetAsync("/projects");
        var p = list.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Empty");
        Assert.Equal(0, p.GetProperty("total").GetInt32());
        Assert.Equal(0, p.GetProperty("done").GetInt32());
        Assert.Equal(0, p.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task List_projects_sorted_by_name_ignoring_case()
    {
        await PostAsync("/projects", new { name = "Zucchini" });
        await PostAsync("/projects", new { name = "apple" });
        await PostAsync("/projects", new { name = "Banana" });
        var list = await GetAsync("/projects");
        var names = list.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray();
        // Case-insensitive: apple < Banana < Zucchini
        Assert.Equal(["apple", "Banana", "Zucchini"], names);
    }

    // ── GET /projects/{id} ───────────────────────────────────────────────────

    [Fact]
    public async Task Get_project_returns_404_for_unknown_id()
    {
        await GetAsync("/projects/99999", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_project_includes_total_done_and_percentDone()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var i1 = await CreateItemAsync("Done Task");
        var i2 = await CreateItemAsync("Open Task");
        await SendAsync(HttpMethod.Put, $"/items/{i1}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{i2}/project", new { projectId = pId }, HttpStatusCode.OK);
        await MarkDoneAsync(i1);
        var project = await GetAsync($"/projects/{pId}");
        Assert.Equal(2, project.GetProperty("total").GetInt32());
        Assert.Equal(1, project.GetProperty("done").GetInt32());
        Assert.Equal(50, project.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Get_project_lists_open_items_before_done_items()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iOpen = await CreateItemAsync("Open Item");  // added first, lower id
        var iDone = await CreateItemAsync("Done Item");  // added second, higher id
        var iOther = await CreateItemAsync("Other");     // not in project (exclusion check)
        await SendAsync(HttpMethod.Put, $"/items/{iOpen}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iDone}/project", new { projectId = pId }, HttpStatusCode.OK);
        await MarkDoneAsync(iDone);
        var project = await GetAsync($"/projects/{pId}");
        // Done Item has a higher id; if sorted by id desc it would come first, but open must precede done
        Assert.Equal(["Open Item", "Done Item"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_open_items_most_recently_added_come_first()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var i1 = await CreateItemAsync("Older Open");
        var i2 = await CreateItemAsync("Newer Open");
        var iOther = await CreateItemAsync("Not In Project"); // exclusion check
        await SendAsync(HttpMethod.Put, $"/items/{i1}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{i2}/project", new { projectId = pId }, HttpStatusCode.OK);
        var project = await GetAsync($"/projects/{pId}");
        Assert.Equal(["Newer Open", "Older Open"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_done_items_most_recently_added_come_first()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iOpen = await CreateItemAsync("Still Open");
        var iDone1 = await CreateItemAsync("Older Done");   // added first
        var iDone2 = await CreateItemAsync("Newer Done");   // added second
        var iOther = await CreateItemAsync("Not In Project"); // exclusion check
        await SendAsync(HttpMethod.Put, $"/items/{iOpen}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iDone1}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{iDone2}/project", new { projectId = pId }, HttpStatusCode.OK);
        await MarkDoneAsync(iDone1);
        await MarkDoneAsync(iDone2);
        var project = await GetAsync($"/projects/{pId}");
        // Open first; then done: Newer Done (added later) before Older Done (added earlier)
        Assert.Equal(["Still Open", "Newer Done", "Older Done"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_excludes_unassigned_items()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        await CreateItemAsync("No Project");  // not assigned to any project
        var iIn = await CreateItemAsync("In Project");
        await SendAsync(HttpMethod.Put, $"/items/{iIn}/project", new { projectId = pId }, HttpStatusCode.OK);
        var project = await GetAsync($"/projects/{pId}");
        Assert.Equal(["In Project"], Titles(project.GetProperty("items")));
    }

    // ── DELETE /projects/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Delete_empty_project_returns_204()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        await SendAsync(HttpMethod.Delete, $"/projects/{pId}", null, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Delete, $"/projects/{pId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_unknown_project_returns_404()
    {
        await SendAsync(HttpMethod.Delete, "/projects/99999", null, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_project_with_items_does_not_remove_the_project()
    {
        var pId = (await PostAsync("/projects", new { name = "Garden" })).GetProperty("id").GetInt32();
        var iId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{iId}/project", new { projectId = pId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Delete, $"/projects/{pId}", null, HttpStatusCode.Conflict);
        await GetAsync($"/projects/{pId}"); // must still be there
    }
}
