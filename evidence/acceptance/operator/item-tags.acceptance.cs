// Brief: briefs/item-tags.md. Written before the run, from the brief only.
using System.Net;
using System.Text.Json;
namespace Acceptance;

public sealed class ItemTagsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<string[]> SetTags(int id, string[] tags, HttpStatusCode expect = HttpStatusCode.OK)
    {
        var r = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags }, expect);
        return expect == HttpStatusCode.OK ? Strings(r) : [];
    }

    private async Task<string[]> GetTags(int id) => Strings(await GetAsync($"/items/{id}/tags"));

    private static string[] Strings(JsonElement a) => a.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private async Task<(string name, int items)[]> AllTags() =>
        (await GetAsync("/tags")).EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("items").GetInt32())).ToArray();

    [Fact]
    public async Task Put_normalises_dedupes_and_sorts()
    {
        var id = await CreateItemAsync("a");
        Assert.Equal(["home", "urgent"], await SetTags(id, ["Urgent", " home ", "HOME"]));
        Assert.Equal(["home", "urgent"], await GetTags(id));
    }

    [Fact]
    public async Task Put_replaces_and_empty_clears()
    {
        var id = await CreateItemAsync("a");
        await SetTags(id, ["home", "work"]);
        Assert.Equal(["urgent"], await SetTags(id, ["urgent"]));
        Assert.Equal(["urgent"], await GetTags(id));
        Assert.Empty(await SetTags(id, []));
        Assert.Empty(await GetTags(id));
    }

    [Fact]
    public async Task New_item_has_no_tags() => Assert.Empty(await GetTags(await CreateItemAsync("a")));

    [Fact]
    public async Task Unknown_item_is_404()
    {
        await SetTags(999999, ["home"], HttpStatusCode.NotFound);
        await GetAsync("/items/999999/tags", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_tags_are_400_and_leave_tags_unchanged()
    {
        var id = await CreateItemAsync("a");
        await SetTags(id, ["keep"]);
        await SetTags(id, ["ok", "   "], HttpStatusCode.BadRequest);
        await SetTags(id, ["ok", new string('x', 31)], HttpStatusCode.BadRequest);
        await SetTags(id, Enumerable.Range(1, 11).Select(i => $"t{i}").ToArray(), HttpStatusCode.BadRequest);
        Assert.Equal(["keep"], await GetTags(id));
    }

    [Fact]
    public async Task Boundaries_thirty_chars_after_trim_and_ten_tags_are_allowed()
    {
        var id = await CreateItemAsync("a");
        var thirty = new string('x', 30);
        Assert.Equal([thirty], await SetTags(id, ["  " + thirty + "  "]));
        var ten = Enumerable.Range(0, 10).Select(i => $"t{i}").ToArray();
        Assert.Equal(ten, await SetTags(id, ten));
    }

    [Fact]
    public async Task List_filters_by_tag_case_insensitive_newest_first()
    {
        var a = await CreateItemAsync("a");
        var b = await CreateItemAsync("b");
        var c = await CreateItemAsync("c");
        await SetTags(a, ["home"]);
        await SetTags(b, ["work"]);
        await SetTags(c, ["home", "work"]);
        Assert.Equal(["c", "a"], Titles(await GetAsync("/items?tag=HOME")));
        Assert.Empty(Titles(await GetAsync("/items?tag=nothing")));
    }

    [Fact]
    public async Task Tag_filter_combines_with_open_and_search()
    {
        var milk = await CreateItemAsync("buy milk");
        var bread = await CreateItemAsync("buy bread");
        var oldMilk = await CreateItemAsync("milk old");
        var workMilk = await CreateItemAsync("milk at work");
        await SetTags(milk, ["home"]);
        await SetTags(bread, ["home"]);
        await SetTags(oldMilk, ["home"]);
        await SetTags(workMilk, ["work"]);
        await MarkDoneAsync(oldMilk);
        Assert.Equal(["buy milk"], Titles(await GetAsync("/items?tag=home&open=true&q=milk")));
        Assert.Equal(["milk old", "buy milk"], Titles(await GetAsync("/items?tag=home&q=milk")));
    }

    [Fact]
    public async Task Tags_endpoint_counts_items_including_done_sorted_and_drops_unused()
    {
        var a = await CreateItemAsync("a");
        var b = await CreateItemAsync("b");
        var c = await CreateItemAsync("c");
        await SetTags(a, ["work", "home"]);
        await SetTags(b, ["home", "zzz"]);
        await SetTags(c, ["home"]);
        await MarkDoneAsync(c);
        Assert.Equal([("home", 3), ("work", 1), ("zzz", 1)], await AllTags());
        await SetTags(b, ["home"]);
        Assert.Equal([("home", 3), ("work", 1)], await AllTags());
    }

    [Fact]
    public async Task Unfiltered_list_still_returns_everything()
    {
        var a = await CreateItemAsync("a");
        await CreateItemAsync("b");
        await SetTags(a, ["home"]);
        Assert.Equal(["b", "a"], Titles(await GetAsync("/items")));
    }
}
