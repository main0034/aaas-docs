using System.Net;
using System.Text.Json;

namespace Acceptance;

public sealed class TagsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<JsonElement> PutTagsAsync(
        int id,
        string[] tags,
        HttpStatusCode expect = HttpStatusCode.OK
    ) => await SendAsync(HttpMethod.Put, $"/items/{id}/tags", new { tags }, expect);

    private async Task<JsonElement> GetItemTagsAsync(
        int id,
        HttpStatusCode expect = HttpStatusCode.OK
    ) => await GetAsync($"/items/{id}/tags", expect);

    private static string[] TagStrings(JsonElement tagsArray) =>
        tagsArray.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static string[] TagEntryNames(JsonElement tagsListArray) =>
        tagsListArray.EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task Put_tags_returns_sorted_alphabetically()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await PutTagsAsync(id, ["urgent", "home", "work"]);

        // Assert
        Assert.Equal(["home", "urgent", "work"], TagStrings(result));
    }

    [Fact]
    public async Task Put_tags_unknown_item_returns_404()
    {
        // Act & Assert
        await PutTagsAsync(999999, ["home"], HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_empty_tags_array_removes_all_tags()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        await PutTagsAsync(id, ["home"]);

        // Act
        var result = await PutTagsAsync(id, []);

        // Assert
        Assert.Equal([], TagStrings(result));
    }

    [Fact]
    public async Task Put_tags_replaces_existing_tags()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        await PutTagsAsync(id, ["home", "urgent"]);

        // Act
        var result = await PutTagsAsync(id, ["work"]);

        // Assert
        Assert.Equal(["work"], TagStrings(result));
    }

    [Fact]
    public async Task Put_tag_stored_lowercased_and_trimmed()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await PutTagsAsync(id, [" Home "]);

        // Assert
        Assert.Equal(["home"], TagStrings(result));
    }

    [Fact]
    public async Task Put_tags_deduplicated_after_normalisation()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await PutTagsAsync(id, ["Home", "home", "HOME"]);

        // Assert
        Assert.Equal(["home"], TagStrings(result));
    }

    [Fact]
    public async Task Put_exact_duplicate_tags_kept_once()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await PutTagsAsync(id, ["work", "work"]);

        // Assert
        Assert.Equal(["work"], TagStrings(result));
    }

    [Fact]
    public async Task Put_tag_of_one_character_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await PutTagsAsync(id, ["a"]);

        // Assert
        Assert.Equal(["a"], TagStrings(result));
    }

    [Fact]
    public async Task Put_tag_of_30_characters_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        var tag = new string('a', 30);

        // Act
        var result = await PutTagsAsync(id, [tag]);

        // Assert
        Assert.Equal([tag], TagStrings(result));
    }

    [Fact]
    public async Task Put_tag_trimmed_to_30_characters_is_accepted()
    {
        // Arrange — 31 raw chars with one leading space; trims to exactly 30, validating trim-before-length
        var id = await CreateItemAsync("Task One");
        var tag = " " + new string('a', 30);

        // Act
        var result = await PutTagsAsync(id, [tag]);

        // Assert
        Assert.Equal([new string('a', 30)], TagStrings(result));
    }

    [Fact]
    public async Task Put_tag_empty_after_trimming_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act & Assert
        await PutTagsAsync(id, ["   "], HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_tag_of_31_characters_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        var tag = new string('a', 31);

        // Act & Assert
        await PutTagsAsync(id, [tag], HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ten_tags_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        var tags = Enumerable.Range(1, 10).Select(i => $"tag{i:D2}").ToArray();

        // Act
        var result = await PutTagsAsync(id, tags);

        // Assert
        Assert.Equal(10, TagStrings(result).Length);
    }

    [Fact]
    public async Task Put_eleven_tags_is_refused()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i:D2}").ToArray();

        // Act & Assert
        await PutTagsAsync(id, tags, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_invalid_tags_item_keeps_existing_tags()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        await PutTagsAsync(id, ["home"]);

        // Act — one valid tag paired with one over-length tag; entire request must be refused
        await PutTagsAsync(id, ["home_new", new string('a', 31)], HttpStatusCode.BadRequest);

        // Assert
        var tags = await GetItemTagsAsync(id);
        Assert.Equal(["home"], TagStrings(tags));
    }

    [Fact]
    public async Task Get_item_tags_returns_sorted_array()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");
        await PutTagsAsync(id, ["work", "home", "urgent"]);

        // Act
        var result = await GetItemTagsAsync(id);

        // Assert
        Assert.Equal(["home", "urgent", "work"], TagStrings(result));
    }

    [Fact]
    public async Task Get_item_tags_for_untagged_item_returns_empty_array()
    {
        // Arrange
        var id = await CreateItemAsync("Task One");

        // Act
        var result = await GetItemTagsAsync(id);

        // Assert
        Assert.Equal([], TagStrings(result));
    }

    [Fact]
    public async Task Get_item_tags_unknown_item_returns_404()
    {
        // Act & Assert
        await GetItemTagsAsync(999999, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_items_by_tag_returns_only_tagged_items()
    {
        // Arrange
        var taggedId = await CreateItemAsync("Home Task");
        await CreateItemAsync("Work Task");
        await PutTagsAsync(taggedId, ["home"]);

        // Act
        var list = await GetAsync("/items?tag=home");

        // Assert
        Assert.Equal(["Home Task"], Titles(list));
    }

    [Fact]
    public async Task Get_items_by_tag_is_case_insensitive()
    {
        // Arrange
        var id = await CreateItemAsync("Home Task");
        await PutTagsAsync(id, ["home"]);

        // Act — query uses uppercase; stored tag is lowercase
        var list = await GetAsync("/items?tag=HOME");

        // Assert
        Assert.Equal(["Home Task"], Titles(list));
    }

    [Fact]
    public async Task Get_items_by_tag_combined_with_open_excludes_done_items()
    {
        // Arrange
        var openId = await CreateItemAsync("Open Task");
        var doneId = await CreateItemAsync("Done Task");
        await PutTagsAsync(openId, ["home"]);
        await PutTagsAsync(doneId, ["home"]);
        await MarkDoneAsync(doneId);

        // Act
        var list = await GetAsync("/items?tag=home&open=true");

        // Assert
        Assert.Equal(["Open Task"], Titles(list));
    }

    [Fact]
    public async Task Get_items_by_tag_combined_with_search()
    {
        // Arrange
        var matchId = await CreateItemAsync("Invoice at home");
        var wrongTagId = await CreateItemAsync("Invoice at work");
        var wrongTitleId = await CreateItemAsync("Groceries");
        await PutTagsAsync(matchId, ["home"]);
        await PutTagsAsync(wrongTagId, ["work"]);
        await PutTagsAsync(wrongTitleId, ["home"]);

        // Act
        var list = await GetAsync("/items?tag=home&q=invoice");

        // Assert
        Assert.Equal(["Invoice at home"], Titles(list));
    }

    [Fact]
    public async Task Get_items_by_tag_ordered_newest_first()
    {
        // Arrange — created first, second, third; expected result reverses that order
        var firstId = await CreateItemAsync("First Task");
        var secondId = await CreateItemAsync("Second Task");
        var thirdId = await CreateItemAsync("Third Task");
        await PutTagsAsync(firstId, ["home"]);
        await PutTagsAsync(secondId, ["home"]);
        await PutTagsAsync(thirdId, ["home"]);

        // Act
        var list = await GetAsync("/items?tag=home");

        // Assert
        Assert.Equal(["Third Task", "Second Task", "First Task"], Titles(list));
    }

    [Fact]
    public async Task Get_all_tags_sorted_by_name()
    {
        // Arrange
        var idA = await CreateItemAsync("Task A");
        var idB = await CreateItemAsync("Task B");
        await PutTagsAsync(idA, ["work"]);
        await PutTagsAsync(idB, ["home"]);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        Assert.Equal(["home", "work"], TagEntryNames(result));
    }

    [Fact]
    public async Task Get_all_tags_item_count_includes_done_items()
    {
        // Arrange — one open and one done item, both carrying "home"
        var openId = await CreateItemAsync("Open Task");
        var doneId = await CreateItemAsync("Done Task");
        await PutTagsAsync(openId, ["home"]);
        await PutTagsAsync(doneId, ["home"]);
        await MarkDoneAsync(doneId);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        var homeEntry = result
            .EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "home");
        Assert.Equal(2, homeEntry.GetProperty("items").GetInt32());
    }

    [Fact]
    public async Task Get_all_tags_excludes_tags_no_item_carries()
    {
        // Arrange
        var id = await CreateItemAsync("Task A");
        await PutTagsAsync(id, ["home"]);
        await PutTagsAsync(id, []); // replace with empty set — "home" now has no carriers

        // Act
        var result = await GetAsync("/tags");

        // Assert
        Assert.Equal([], TagEntryNames(result));
    }
}
