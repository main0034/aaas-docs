using System.Net;
using System.Text.Json;

namespace Acceptance;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<JsonElement> CreateProjectAsync(string name, HttpStatusCode expect = HttpStatusCode.Created)
        => await PostAsync("/projects", new { name }, expect);

    private async Task<int> CreateProjectIdAsync(string name)
        => (await CreateProjectAsync(name)).GetProperty("id").GetInt32();

    private async Task<JsonElement> AssignAsync(int itemId, int? projectId, HttpStatusCode expect = HttpStatusCode.OK)
        => await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, expect);

    // POST /projects — creation and name validation

    [Fact]
    public async Task Create_project_returns_id_and_name()
    {
        var proj = await CreateProjectAsync("Garden");
        Assert.True(proj.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Garden", proj.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_whitespace_only_name_is_rejected()
        => await CreateProjectAsync("   ", HttpStatusCode.BadRequest);

    [Fact]
    public async Task Create_project_single_char_name_is_accepted()
        => await CreateProjectAsync("X");

    [Fact]
    public async Task Create_project_name_of_100_chars_is_accepted()
        => await CreateProjectAsync(new string('a', 100));

    [Fact]
    public async Task Create_project_name_of_101_chars_is_rejected()
        => await CreateProjectAsync(new string('a', 101), HttpStatusCode.BadRequest);

    [Fact]
    public async Task Create_project_name_is_stored_trimmed()
    {
        var proj = await CreateProjectAsync("  Garden  ");
        Assert.Equal("Garden", proj.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_padded_name_of_100_chars_after_trim_is_accepted()
        // 104 chars total, 100 after trimming — length rule applies post-trim
        => await CreateProjectAsync("  " + new string('a', 100) + "  ");

    [Fact]
    public async Task Create_project_padded_name_of_101_chars_after_trim_is_rejected()
        // 105 chars total, 101 after trimming — still rejected
        => await CreateProjectAsync("  " + new string('a', 101) + "  ", HttpStatusCode.BadRequest);

    [Fact]
    public async Task Create_project_duplicate_name_returns_409()
    {
        await CreateProjectAsync("Garden");
        await CreateProjectAsync("Garden", HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_duplicate_name_different_case_returns_409()
    {
        await CreateProjectAsync("Garden");
        await CreateProjectAsync("GARDEN", HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_rejected_duplicate_does_not_create_second_project()
    {
        await CreateProjectAsync("Garden");
        await CreateProjectAsync("Garden", HttpStatusCode.Conflict);
        var list = await GetAsync("/projects");
        Assert.Single(list.EnumerateArray());
    }

    // PUT /items/{id}/project — assigning and removing

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        var itemId = await CreateItemAsync("Task");
        var projId = await CreateProjectIdAsync("Garden");
        var item = await AssignAsync(itemId, projId);
        Assert.Equal(itemId, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Remove_item_from_project_with_null_project_id_returns_200()
    {
        var itemId = await CreateItemAsync("Task");
        var projId = await CreateProjectIdAsync("Garden");
        await AssignAsync(itemId, projId);
        var item = await AssignAsync(itemId, null);
        Assert.Equal(itemId, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Assign_item_to_unknown_project_returns_400()
    {
        var itemId = await CreateItemAsync("Task");
        await AssignAsync(itemId, 99999, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assign_unknown_item_to_project_returns_404()
    {
        var projId = await CreateProjectIdAsync("Garden");
        await AssignAsync(99999, projId, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Moving_item_to_another_project_removes_it_from_original()
    {
        var projA = await CreateProjectIdAsync("A");
        var projB = await CreateProjectIdAsync("B");
        var itemId = await CreateItemAsync("Task");
        await AssignAsync(itemId, projA);
        await AssignAsync(itemId, projB);

        var a = await GetAsync($"/projects/{projA}");
        var b = await GetAsync($"/projects/{projB}");
        Assert.Empty(a.GetProperty("items").EnumerateArray());
        Assert.Equal(["Task"], Titles(b.GetProperty("items")));
    }

    // GET /projects — list with stats

    [Fact]
    public async Task List_projects_sorted_by_name_case_insensitively()
    {
        // Inserted in non-sorted order; sort key decides every adjacent pair
        await CreateProjectAsync("Bravo");
        await CreateProjectAsync("alpha");
        await CreateProjectAsync("CHARLIE");
        var list = await GetAsync("/projects");
        var names = list.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToArray();
        Assert.Equal(["alpha", "Bravo", "CHARLIE"], names);
    }

    [Fact]
    public async Task List_projects_shows_total_done_and_percent_done()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var ids = new int[4];
        for (var i = 0; i < 4; i++)
        {
            ids[i] = await CreateItemAsync($"Item {i}");
            await AssignAsync(ids[i], projId);
        }
        await MarkDoneAsync(ids[0]); // 1 of 4 done = 25%

        var proj = (await GetAsync("/projects")).EnumerateArray().Single();
        Assert.Equal(4, proj.GetProperty("total").GetInt32());
        Assert.Equal(1, proj.GetProperty("done").GetInt32());
        Assert.Equal(25, proj.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Project_percent_done_rounds_down()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var ids = new int[3];
        for (var i = 0; i < 3; i++)
        {
            ids[i] = await CreateItemAsync($"Item {i}");
            await AssignAsync(ids[i], projId);
        }
        await MarkDoneAsync(ids[0]); // 1 of 3 = 33.33% → floor = 33

        var proj = (await GetAsync("/projects")).EnumerateArray().Single();
        Assert.Equal(33, proj.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Project_with_no_items_has_percent_done_zero()
    {
        await CreateProjectAsync("Garden");
        var proj = (await GetAsync("/projects")).EnumerateArray().Single();
        Assert.Equal(0, proj.GetProperty("total").GetInt32());
        Assert.Equal(0, proj.GetProperty("done").GetInt32());
        Assert.Equal(0, proj.GetProperty("percentDone").GetInt32());
    }

    // GET /projects/{id} — single project with items

    [Fact]
    public async Task Get_project_returns_404_for_unknown_id()
        => await GetAsync("/projects/99999", HttpStatusCode.NotFound);

    [Fact]
    public async Task Get_project_includes_id_name_and_stats()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var proj = await GetAsync($"/projects/{projId}");
        Assert.Equal(projId, proj.GetProperty("id").GetInt32());
        Assert.Equal("Garden", proj.GetProperty("name").GetString());
        Assert.True(proj.TryGetProperty("total", out _));
        Assert.True(proj.TryGetProperty("done", out _));
        Assert.True(proj.TryGetProperty("percentDone", out _));
    }

    [Fact]
    public async Task Get_project_lists_open_items_before_done_newest_first()
    {
        // Inserted in order: A (→done), B (open), C (open), D (→done)
        // Expected: C (newest open), B (older open), D (newest done), A (oldest done)
        var projId = await CreateProjectIdAsync("Garden");
        var a = await CreateItemAsync("A");
        var b = await CreateItemAsync("B");
        var c = await CreateItemAsync("C");
        var d = await CreateItemAsync("D");
        foreach (var id in new[] { a, b, c, d })
        {
            await AssignAsync(id, projId);
        }
        await MarkDoneAsync(a);
        await MarkDoneAsync(d);

        var proj = await GetAsync($"/projects/{projId}");
        Assert.Equal(["C", "B", "D", "A"], Titles(proj.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_does_not_include_items_from_other_projects()
    {
        var projId = await CreateProjectIdAsync("Mine");
        var otherId = await CreateProjectIdAsync("Other");
        var mine = await CreateItemAsync("Mine");
        var other = await CreateItemAsync("Other");
        await AssignAsync(mine, projId);
        await AssignAsync(other, otherId);

        var proj = await GetAsync($"/projects/{projId}");
        Assert.Equal(["Mine"], Titles(proj.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_unassigned_item_no_longer_appears()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var itemId = await CreateItemAsync("Task");
        await AssignAsync(itemId, projId);
        await AssignAsync(itemId, null); // remove

        var proj = await GetAsync($"/projects/{projId}");
        Assert.Empty(proj.GetProperty("items").EnumerateArray());
    }

    // DELETE /projects/{id}

    [Fact]
    public async Task Delete_empty_project_returns_204_and_project_is_gone()
    {
        var projId = await CreateProjectIdAsync("Garden");
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.NoContent);
        await GetAsync($"/projects/{projId}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var itemId = await CreateItemAsync("Task");
        await AssignAsync(itemId, projId);
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_project_with_items_rejected_project_still_exists()
    {
        var projId = await CreateProjectIdAsync("Garden");
        var itemId = await CreateItemAsync("Task");
        await AssignAsync(itemId, projId);
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.Conflict);
        await GetAsync($"/projects/{projId}"); // still 200
    }

    [Fact]
    public async Task Delete_unknown_project_returns_404()
        => await SendAsync(HttpMethod.Delete, "/projects/99999", null, HttpStatusCode.NotFound);
}
