using System.Net;
using System.Text.Json;

namespace Acceptance;

public sealed class TagsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    [Fact]
    public async Task Put_tags_returns_200_with_sorted_array()
    {
        var id = await CreateItemAsync("item");
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "work", "home" } }, HttpStatusCode.OK);
        Assert.Equal(["home", "work"], TagStrings(result));
    }

    [Fact]
    public async Task Get_item_tags_returns_sorted_array()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "work", "home" } }, HttpStatusCode.OK);
        Assert.Equal(["home", "work"], TagStrings(await GetAsync($"/items/{id}/tags")));
    }

    [Fact]
    public async Task Put_tags_replaces_previous_tags()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "old" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "new" } }, HttpStatusCode.OK);
        Assert.Equal(["new"], TagStrings(await GetAsync($"/items/{id}/tags")));
    }

    [Fact]
    public async Task Tags_are_trimmed_and_lowercased()
    {
        var id = await CreateItemAsync("item");
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { " Home " } }, HttpStatusCode.OK);
        Assert.Equal(["home"], TagStrings(result));
    }

    [Fact]
    public async Task Case_variant_duplicates_are_deduplicated()
    {
        var id = await CreateItemAsync("item");
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home", "HOME" } }, HttpStatusCode.OK);
        Assert.Equal(["home"], TagStrings(result));
    }

    [Fact]
    public async Task Literal_duplicate_tags_kept_once()
    {
        var id = await CreateItemAsync("item");
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home", "home" } }, HttpStatusCode.OK);
        Assert.Equal(["home"], TagStrings(result));
    }

    [Fact]
    public async Task Put_empty_array_removes_all_tags()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = Array.Empty<string>() }, HttpStatusCode.OK);
        Assert.Equal(0, (await GetAsync($"/items/{id}/tags")).GetArrayLength());
    }

    [Fact]
    public async Task Put_tags_on_unknown_item_returns_404()
    {
        await SendAsync(HttpMethod.Put, "/items/999999/tags", new { tags = new[] { "home" } }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_tags_on_unknown_item_returns_404()
    {
        await GetAsync("/items/999999/tags", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tag_that_trims_to_empty_string_is_refused()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "   " } }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tag_of_31_chars_is_refused()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { new string('a', 31) } }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tag_of_30_chars_is_accepted()
    {
        var id = await CreateItemAsync("item");
        var tag30 = new string('a', 30);
        Assert.Equal([tag30], TagStrings(await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { tag30 } }, HttpStatusCode.OK)));
    }

    [Fact]
    public async Task Tag_with_spaces_trimming_to_30_chars_is_accepted()
    {
        var id = await CreateItemAsync("item");
        var tag = " " + new string('b', 30); // 31 chars raw, 30 after trim
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { tag } }, HttpStatusCode.OK);
        Assert.Equal([new string('b', 30)], TagStrings(result));
    }

    [Fact]
    public async Task Tag_is_stored_as_trimmed_form()
    {
        var id = await CreateItemAsync("item");
        var result = await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { " a" } }, HttpStatusCode.OK);
        Assert.Equal(["a"], TagStrings(result));
    }

    [Fact]
    public async Task Eleven_tags_is_refused()
    {
        var id = await CreateItemAsync("item");
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i:D2}").ToArray();
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ten_tags_is_accepted()
    {
        var id = await CreateItemAsync("item");
        var tags = Enumerable.Range(1, 10).Select(i => $"tag{i:D2}").ToArray();
        Assert.Equal(10, (await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags }, HttpStatusCode.OK)).GetArrayLength());
    }

    [Fact]
    public async Task Refused_put_keeps_existing_tags()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { new string('x', 31) } }, HttpStatusCode.BadRequest);
        Assert.Equal(["home"], TagStrings(await GetAsync($"/items/{id}/tags")));
    }

    [Fact]
    public async Task Items_filtered_by_tag_excludes_untagged()
    {
        var tagged = await CreateItemAsync("tagged");
        await CreateItemAsync("untagged");
        await SendAsync(HttpMethod.Put, $"/items/{tagged}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        Assert.Equal(["tagged"], Titles(await GetAsync("/items?tag=home")));
    }

    [Fact]
    public async Task Tag_filter_is_case_insensitive()
    {
        var id = await CreateItemAsync("tagged");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        Assert.Equal(["tagged"], Titles(await GetAsync("/items?tag=HOME")));
    }

    [Fact]
    public async Task Tag_filter_orders_items_newest_first()
    {
        var first = await CreateItemAsync("first");
        var second = await CreateItemAsync("second");
        await SendAsync(HttpMethod.Put, $"/items/{first}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{second}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        Assert.Equal(["second", "first"], Titles(await GetAsync("/items?tag=home")));
    }

    [Fact]
    public async Task Tag_filter_combined_with_open_excludes_done_items()
    {
        var active = await CreateItemAsync("active");
        var done = await CreateItemAsync("done");
        await SendAsync(HttpMethod.Put, $"/items/{active}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{done}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await MarkDoneAsync(done);
        Assert.Equal(["active"], Titles(await GetAsync("/items?tag=home&open=true")));
    }

    [Fact]
    public async Task Tag_filter_combined_with_search()
    {
        var both = await CreateItemAsync("buy milk");
        var tagOnly = await CreateItemAsync("call doctor");
        var searchOnly = await CreateItemAsync("buy eggs");
        await SendAsync(HttpMethod.Put, $"/items/{both}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{tagOnly}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        // searchOnly has no "home" tag; tagOnly title doesn't contain "buy"
        Assert.Equal(["buy milk"], Titles(await GetAsync("/items?tag=home&q=buy")));
    }

    [Fact]
    public async Task Tag_filter_combined_with_open_and_search()
    {
        var openMatch = await CreateItemAsync("buy milk");
        var doneMatch = await CreateItemAsync("buy bread");
        var noTag = await CreateItemAsync("buy eggs");
        var noSearch = await CreateItemAsync("call doctor");
        await SendAsync(HttpMethod.Put, $"/items/{openMatch}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{doneMatch}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{noSearch}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await MarkDoneAsync(doneMatch);
        // doneMatch excluded by open, noTag excluded by tag, noSearch excluded by search
        Assert.Equal(["buy milk"], Titles(await GetAsync("/items?tag=home&open=true&q=buy")));
    }

    [Fact]
    public async Task Get_all_tags_sorted_by_name_with_item_counts()
    {
        var id1 = await CreateItemAsync("item1");
        var id2 = await CreateItemAsync("item2");
        var id3 = await CreateItemAsync("item3");
        await SendAsync(HttpMethod.Put, $"/items/{id1}/tags", new { tags = new[] { "work", "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id2}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id3}/tags", new { tags = new[] { "urgent" } }, HttpStatusCode.OK);
        var tagList = (await GetAsync("/tags")).EnumerateArray().ToList();
        Assert.Equal(["home", "urgent", "work"], tagList.Select(t => t.GetProperty("name").GetString()!).ToArray());
        Assert.Equal(2, tagList[0].GetProperty("items").GetInt32()); // home: id1 and id2
        Assert.Equal(1, tagList[1].GetProperty("items").GetInt32()); // urgent: id3
        Assert.Equal(1, tagList[2].GetProperty("items").GetInt32()); // work: id1
    }

    [Fact]
    public async Task Get_all_tags_counts_done_items()
    {
        var id = await CreateItemAsync("done item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await MarkDoneAsync(id);
        var tagList = (await GetAsync("/tags")).EnumerateArray().ToList();
        Assert.Single(tagList);
        Assert.Equal("home", tagList[0].GetProperty("name").GetString());
        Assert.Equal(1, tagList[0].GetProperty("items").GetInt32());
    }

    [Fact]
    public async Task Get_all_tags_excludes_uncarried_tags()
    {
        var id = await CreateItemAsync("item");
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = new[] { "home" } }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags = Array.Empty<string>() }, HttpStatusCode.OK);
        Assert.Equal(0, (await GetAsync("/tags")).GetArrayLength());
    }

    private static string[] TagStrings(JsonElement e) =>
        e.EnumerateArray().Select(t => t.GetString()!).ToArray();
}
