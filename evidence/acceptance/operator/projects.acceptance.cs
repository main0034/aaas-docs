// Brief: briefs/projects.md. Written before the run, from the brief only.
using System.Net;
using System.Text.Json;
namespace Acceptance;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<int> Project(string name) =>
        (await PostAsync("/projects", new { name })).GetProperty("id").GetInt32();

    private async Task<JsonElement> Assign(int item, int? projectId, HttpStatusCode expect = HttpStatusCode.OK) =>
        await SendAsync(HttpMethod.Put, $"/items/{item}/project", new { projectId }, expect);

    private static (string name, int total, int done, int pct) Summary(JsonElement p) =>
        (p.GetProperty("name").GetString()!, p.GetProperty("total").GetInt32(),
         p.GetProperty("done").GetInt32(), p.GetProperty("percentDone").GetInt32());

    [Fact]
    public async Task Create_returns_201_with_id_and_trimmed_name()
    {
        var p = await PostAsync("/projects", new { name = "  Garden  " });
        Assert.Equal("Garden", p.GetProperty("name").GetString());
        Assert.True(p.GetProperty("id").GetInt32() > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_name_is_400(string name) =>
        await PostAsync("/projects", new { name }, HttpStatusCode.BadRequest);

    [Fact]
    public async Task Name_over_100_is_400_and_100_after_trim_is_allowed()
    {
        await PostAsync("/projects", new { name = new string('x', 101) }, HttpStatusCode.BadRequest);
        await PostAsync("/projects", new { name = " " + new string('y', 100) + " " });
    }

    [Fact]
    public async Task Duplicate_name_ignoring_case_and_spaces_is_409()
    {
        await Project("Garden");
        await PostAsync("/projects", new { name = " gARDEN" }, HttpStatusCode.Conflict);
        Assert.Single((await GetAsync("/projects")).EnumerateArray());
    }

    [Fact]
    public async Task List_is_sorted_by_name_ignoring_case_with_progress()
    {
        var g = await Project("garden");
        var a = await Project("Attic");
        await Project("Basement");
        var i1 = await CreateItemAsync("i1");
        var i2 = await CreateItemAsync("i2");
        var i3 = await CreateItemAsync("i3");
        var i4 = await CreateItemAsync("i4");
        var i5 = await CreateItemAsync("i5");
        foreach (var i in new[] { i1, i2, i3 }) await Assign(i, g);
        await Assign(i4, a);
        await CreateItemAsync("unassigned");
        await MarkDoneAsync(i1);
        await MarkDoneAsync(i4);
        await MarkDoneAsync(i5); // not in any project
        var list = (await GetAsync("/projects")).EnumerateArray().Select(Summary).ToArray();
        Assert.Equal([("Attic", 1, 1, 100), ("Basement", 0, 0, 0), ("garden", 3, 1, 33)], list);
    }

    [Fact]
    public async Task Percent_rounds_down()
    {
        var p = await Project("P");
        var ids = new List<int>();
        for (var i = 0; i < 3; i++) ids.Add(await CreateItemAsync($"i{i}"));
        foreach (var i in ids) await Assign(i, p);
        await MarkDoneAsync(ids[0]);
        await MarkDoneAsync(ids[1]);
        Assert.Equal(("P", 3, 2, 66), Summary(await GetAsync($"/projects/{p}")));
    }

    [Fact]
    public async Task Detail_lists_open_newest_first_then_done_newest_first()
    {
        var p = await Project("P");
        var o1 = await CreateItemAsync("open-old");
        var d1 = await CreateItemAsync("done-old");
        var o2 = await CreateItemAsync("open-new");
        var d2 = await CreateItemAsync("done-new");
        var other = await CreateItemAsync("elsewhere");
        foreach (var i in new[] { o1, d1, o2, d2 }) await Assign(i, p);
        await MarkDoneAsync(d2);
        await MarkDoneAsync(d1);
        var detail = await GetAsync($"/projects/{p}");
        Assert.Equal(["open-new", "open-old", "done-new", "done-old"], Titles(detail.GetProperty("items")));
        Assert.Equal(("P", 4, 2, 50), Summary(detail));
    }

    [Fact]
    public async Task Detail_items_have_the_list_shape()
    {
        var p = await Project("P");
        var id = (await PostAsync("/items", new { title = "x", note = "n", priority = 3 })).GetProperty("id").GetInt32();
        await Assign(id, p);
        var item = (await GetAsync($"/projects/{p}")).GetProperty("items")[0];
        Assert.Equal(id, item.GetProperty("id").GetInt32());
        Assert.Equal("n", item.GetProperty("note").GetString());
        Assert.Equal(3, item.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task Moving_and_unassigning_update_both_projects()
    {
        var a = await Project("A");
        var b = await Project("B");
        var i = await CreateItemAsync("i");
        await Assign(i, a);
        await Assign(i, b);
        Assert.Equal(0, (await GetAsync($"/projects/{a}")).GetProperty("total").GetInt32());
        Assert.Equal(["i"], Titles((await GetAsync($"/projects/{b}")).GetProperty("items")));
        await Assign(i, null);
        Assert.Equal(0, (await GetAsync($"/projects/{b}")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Assign_returns_the_item()
    {
        var p = await Project("P");
        var i = await CreateItemAsync("the-item");
        var r = await Assign(i, p);
        Assert.Equal(i, r.GetProperty("id").GetInt32());
        Assert.Equal("the-item", r.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Assign_unknown_item_404_unknown_project_400()
    {
        var p = await Project("P");
        await Assign(999999, p, HttpStatusCode.NotFound);
        var i = await CreateItemAsync("i");
        await Assign(i, 999999, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_project_detail_is_404() =>
        await GetAsync("/projects/999999", HttpStatusCode.NotFound);

    [Fact]
    public async Task Delete_empty_204_nonempty_409_unknown_404()
    {
        var empty = await Project("Empty");
        var full = await Project("Full");
        await Assign(await CreateItemAsync("i"), full);
        await SendAsync(HttpMethod.Delete, $"/projects/{full}", null, HttpStatusCode.Conflict);
        await SendAsync(HttpMethod.Delete, $"/projects/{empty}", null, HttpStatusCode.NoContent);
        await SendAsync(HttpMethod.Delete, $"/projects/{empty}", null, HttpStatusCode.NotFound);
        Assert.Equal(["Full"], (await GetAsync("/projects")).EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToArray());
    }

    [Fact]
    public async Task Project_with_only_done_items_still_blocks_delete()
    {
        var p = await Project("P");
        var i = await CreateItemAsync("i");
        await Assign(i, p);
        await MarkDoneAsync(i);
        await SendAsync(HttpMethod.Delete, $"/projects/{p}", null, HttpStatusCode.Conflict);
    }
}
