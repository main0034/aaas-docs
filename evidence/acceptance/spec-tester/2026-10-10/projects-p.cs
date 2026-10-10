using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261010Projects;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<int> CreateProjectAsync(string name) =>
        (await PostAsync("/projects", new { name })).GetProperty("id").GetInt32();

    private async Task<JsonElement> AssignItemAsync(int itemId, int? projectId, HttpStatusCode expect = HttpStatusCode.OK) =>
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, expect);

    private static string[] ProjectNames(JsonElement list) =>
        list.EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        var result = await PostAsync("/projects", new { name = "Garden" });
        Assert.True(result.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Garden", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_stores_name_trimmed()
    {
        var result = await PostAsync("/projects", new { name = "  Garden  " });
        Assert.Equal("Garden", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_name_one_char_after_trimming_is_accepted()
    {
        var result = await PostAsync("/projects", new { name = "  A  " });
        Assert.Equal("A", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_name_100_chars_after_trimming_is_accepted()
    {
        var name = new string('x', 100);
        var result = await PostAsync("/projects", new { name });
        Assert.Equal(name, result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_empty_name_after_trimming_returns_400()
    {
        await PostAsync("/projects", new { name = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_name_101_chars_returns_400()
    {
        await PostAsync("/projects", new { name = new string('x', 101) }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_duplicate_name_same_case_returns_409()
    {
        await CreateProjectAsync("Garden");
        await PostAsync("/projects", new { name = "Garden" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_duplicate_name_different_case_returns_409()
    {
        await CreateProjectAsync("Garden");
        await PostAsync("/projects", new { name = "garden" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task After_rejected_duplicate_name_no_project_created()
    {
        await CreateProjectAsync("Garden");
        await PostAsync("/projects", new { name = "GARDEN" }, HttpStatusCode.Conflict);
        var projects = await GetAsync("/projects");
        Assert.Equal(["Garden"], ProjectNames(projects));
    }

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        var projectId = await CreateProjectAsync("Work");
        var itemId = await CreateItemAsync("Task");
        var result = await AssignItemAsync(itemId, projectId);
        Assert.Equal(itemId, result.GetProperty("id").GetInt32());
        Assert.Equal("Task", result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Remove_item_from_project_with_null_returns_200_with_item()
    {
        var projectId = await CreateProjectAsync("Work");
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, projectId);
        var result = await AssignItemAsync(itemId, null);
        Assert.Equal(itemId, result.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Assign_item_unknown_item_returns_404()
    {
        var projectId = await CreateProjectAsync("Work");
        await SendAsync(HttpMethod.Put, "/items/99999/project", new { projectId }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_item_unknown_project_returns_400()
    {
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, 99999, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Move_item_to_different_project_removes_it_from_old_project()
    {
        var projectA = await CreateProjectAsync("Alpha");
        var projectB = await CreateProjectAsync("Beta");
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, projectA);
        await AssignItemAsync(itemId, projectB);
        var detailA = await GetAsync($"/projects/{projectA}");
        Assert.Empty(detailA.GetProperty("items").EnumerateArray());
        var detailB = await GetAsync($"/projects/{projectB}");
        Assert.Equal(["Task"], Titles(detailB.GetProperty("items")));
    }

    [Fact]
    public async Task Get_projects_sorted_by_name_case_insensitive()
    {
        await CreateProjectAsync("Banana");
        await CreateProjectAsync("apple");
        await CreateProjectAsync("Cherry");
        var projects = await GetAsync("/projects");
        Assert.Equal(["apple", "Banana", "Cherry"], ProjectNames(projects));
    }

    [Fact]
    public async Task Get_projects_total_done_and_percentDone_are_correct()
    {
        var projId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("a");
        var item2 = await CreateItemAsync("b");
        var item3 = await CreateItemAsync("c");
        await AssignItemAsync(item1, projId);
        await AssignItemAsync(item2, projId);
        await AssignItemAsync(item3, projId);
        await MarkDoneAsync(item1);
        var proj = (await GetAsync("/projects")).EnumerateArray().First();
        Assert.Equal(projId, proj.GetProperty("id").GetInt32());
        Assert.Equal(3, proj.GetProperty("total").GetInt32());
        Assert.Equal(1, proj.GetProperty("done").GetInt32());
        Assert.Equal(33, proj.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Get_projects_percentDone_rounds_down_not_nearest()
    {
        var projId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("a");
        var item2 = await CreateItemAsync("b");
        var item3 = await CreateItemAsync("c");
        await AssignItemAsync(item1, projId);
        await AssignItemAsync(item2, projId);
        await AssignItemAsync(item3, projId);
        await MarkDoneAsync(item1);
        await MarkDoneAsync(item2);
        var proj = (await GetAsync("/projects")).EnumerateArray().First();
        Assert.Equal(66, proj.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Get_projects_no_items_is_zero_percent()
    {
        await CreateProjectAsync("Empty");
        var proj = (await GetAsync("/projects")).EnumerateArray().First();
        Assert.Equal(0, proj.GetProperty("total").GetInt32());
        Assert.Equal(0, proj.GetProperty("done").GetInt32());
        Assert.Equal(0, proj.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Get_project_unknown_returns_404()
    {
        await GetAsync("/projects/99999", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_project_returns_stats_and_items_in_item_shape()
    {
        var projId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("task-a");
        var item2 = await CreateItemAsync("task-b");
        await AssignItemAsync(item1, projId);
        await AssignItemAsync(item2, projId);
        await MarkDoneAsync(item1);
        var detail = await GetAsync($"/projects/{projId}");
        Assert.Equal("Work", detail.GetProperty("name").GetString());
        Assert.Equal(2, detail.GetProperty("total").GetInt32());
        Assert.Equal(1, detail.GetProperty("done").GetInt32());
        Assert.Equal(50, detail.GetProperty("percentDone").GetInt32());
        var items = detail.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var first = items.EnumerateArray().First();
        first.GetProperty("id");
        first.GetProperty("title");
        first.GetProperty("isDone");
    }

    [Fact]
    public async Task Get_project_open_items_before_done_newest_first_within_group()
    {
        var projId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("open-old");
        var item2 = await CreateItemAsync("open-new");
        var item3 = await CreateItemAsync("done-old");
        var item4 = await CreateItemAsync("done-new");
        await AssignItemAsync(item1, projId);
        await AssignItemAsync(item2, projId);
        await AssignItemAsync(item3, projId);
        await AssignItemAsync(item4, projId);
        await MarkDoneAsync(item3);
        await MarkDoneAsync(item4);
        var detail = await GetAsync($"/projects/{projId}");
        Assert.Equal(["open-new", "open-old", "done-new", "done-old"], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Delete_project_with_no_items_returns_204()
    {
        var projId = await CreateProjectAsync("Empty");
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        var projId = await CreateProjectAsync("Work");
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, projId);
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_project_with_only_done_items_returns_409()
    {
        var projId = await CreateProjectAsync("Work");
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, projId);
        await MarkDoneAsync(itemId);
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_project_unknown_returns_404()
    {
        await SendAsync(HttpMethod.Delete, "/projects/99999", null, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task After_delete_rejected_project_still_exists()
    {
        var projId = await CreateProjectAsync("Work");
        var itemId = await CreateItemAsync("Task");
        await AssignItemAsync(itemId, projId);
        await SendAsync(HttpMethod.Delete, $"/projects/{projId}", null, HttpStatusCode.Conflict);
        await GetAsync($"/projects/{projId}");
    }
}
