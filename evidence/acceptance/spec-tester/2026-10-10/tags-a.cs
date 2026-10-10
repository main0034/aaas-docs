using System.Net;
using System.Text.Json;
using App.Tests.Postgres;

namespace Acceptance;

public sealed class TagsAcceptance(TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<string[]> PutTagsAsync(
        int id,
        string[] tags,
        HttpStatusCode expect = HttpStatusCode.OK
    )
    {
        var result = await SendAsync(
            HttpMethod.Put,
            $"/items/{id}/tags",
            new { tags },
            expect
        );
        return expect == HttpStatusCode.OK ? ParseStringArray(result) : [];
    }

    private async Task<string[]> GetItemTagsAsync(
        int id,
        HttpStatusCode expect = HttpStatusCode.OK
    )
    {
        var result = await GetAsync($"/items/{id}/tags", expect);
        return expect == HttpStatusCode.OK ? ParseStringArray(result) : [];
    }

    private static string[] ParseStringArray(JsonElement el) =>
        el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : [];

    [Fact]
    public async Task Put_tags_returns_tags_sorted_alphabetically()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await PutTagsAsync(id, ["urgent", "home", "work"]);

        // Assert
        Assert.Equal(["home", "urgent", "work"], tags);
    }

    [Fact]
    public async Task Put_tags_normalizes_to_lowercase()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await PutTagsAsync(id, ["Home", "URGENT"]);

        // Assert
        Assert.Equal(["home", "urgent"], tags);
    }

    [Fact]
    public async Task Put_tags_trims_surrounding_spaces()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await PutTagsAsync(id, [" home ", "work"]);

        // Assert
        Assert.Equal(["home", "work"], tags);
    }

    [Fact]
    public async Task Put_tags_length_is_measured_after_trimming()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        // 31 chars total, 30 after trimming the leading space — valid only after trim
        var paddedTag = " " + new string('a', 30);

        // Act
        var tags = await PutTagsAsync(id, [paddedTag]);

        // Assert
        Assert.Equal([new string('a', 30)], tags);
    }

    [Fact]
    public async Task Put_tags_deduplicates_after_normalization()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await PutTagsAsync(id, ["home", "HOME", " Home "]);

        // Assert
        Assert.Equal(["home"], tags);
    }

    [Fact]
    public async Task Put_tags_empty_list_removes_all_tags()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["home"]);

        // Act
        var tags = await PutTagsAsync(id, []);

        // Assert
        Assert.Equal([], tags);
    }

    [Fact]
    public async Task Put_tags_replaces_previous_tags()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["home", "work"]);

        // Act
        var tags = await PutTagsAsync(id, ["urgent"]);

        // Assert
        Assert.Equal(["urgent"], tags);
    }

    [Fact]
    public async Task Put_tags_tag_of_1_char_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await PutTagsAsync(id, ["a"]);

        // Assert
        Assert.Equal(["a"], tags);
    }

    [Fact]
    public async Task Put_tags_tag_of_30_chars_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        var tag = new string('a', 30);

        // Act
        var tags = await PutTagsAsync(id, [tag]);

        // Assert
        Assert.Equal([tag], tags);
    }

    [Fact]
    public async Task Put_tags_tag_of_31_chars_is_400()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act & Assert
        await PutTagsAsync(id, [new string('a', 31)], HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_tags_whitespace_only_tag_is_400()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act & Assert
        await PutTagsAsync(id, ["   "], HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_tags_10_tags_is_accepted()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        var tags = Enumerable.Range(1, 10).Select(i => $"tag{i:00}").ToArray();

        // Act
        var result = await PutTagsAsync(id, tags);

        // Assert
        Assert.Equal(10, result.Length);
    }

    [Fact]
    public async Task Put_tags_11_tags_is_400()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i:00}").ToArray();

        // Act & Assert
        await PutTagsAsync(id, tags, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_tags_invalid_does_not_change_existing_tags()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["home"]);

        // Act
        await PutTagsAsync(id, [new string('a', 31)], HttpStatusCode.BadRequest);

        // Assert
        var tags = await GetItemTagsAsync(id);
        Assert.Equal(["home"], tags);
    }

    [Fact]
    public async Task Put_tags_unknown_item_is_404()
    {
        // Act & Assert
        await SendAsync(
            HttpMethod.Put,
            "/items/999/tags",
            new { tags = new[] { "home" } },
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task Get_item_tags_returns_sorted_alphabetically()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["work", "home"]);

        // Act
        var tags = await GetItemTagsAsync(id);

        // Assert
        Assert.Equal(["home", "work"], tags);
    }

    [Fact]
    public async Task Get_item_tags_for_untagged_item_returns_empty_array()
    {
        // Arrange
        var id = await CreateItemAsync("task");

        // Act
        var tags = await GetItemTagsAsync(id);

        // Assert
        Assert.Equal([], tags);
    }

    [Fact]
    public async Task Get_item_tags_unknown_item_is_404()
    {
        // Act & Assert
        await GetAsync("/items/999/tags", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Items_filter_by_tag_returns_only_matching_items()
    {
        // Arrange
        var idHome = await CreateItemAsync("home task");
        await PutTagsAsync(idHome, ["home"]);
        var idWork = await CreateItemAsync("work task");
        await PutTagsAsync(idWork, ["work"]);
        await CreateItemAsync("untagged task");

        // Act
        var list = await GetAsync("/items?tag=home");

        // Assert
        Assert.Equal(["home task"], Titles(list));
    }

    [Fact]
    public async Task Items_filter_by_tag_is_case_insensitive()
    {
        // Arrange
        var id = await CreateItemAsync("tagged");
        await PutTagsAsync(id, ["home"]);
        await CreateItemAsync("untagged");

        // Act
        var list = await GetAsync("/items?tag=HOME");

        // Assert
        Assert.Equal(["tagged"], Titles(list));
    }

    [Fact]
    public async Task Items_filter_by_tag_combined_with_open_excludes_done_items()
    {
        // Arrange
        var idOpen = await CreateItemAsync("open tagged");
        await PutTagsAsync(idOpen, ["home"]);
        var idDone = await CreateItemAsync("done tagged");
        await PutTagsAsync(idDone, ["home"]);
        await MarkDoneAsync(idDone);

        // Act
        var list = await GetAsync("/items?tag=home&open=true");

        // Assert
        Assert.Equal(["open tagged"], Titles(list));
    }

    [Fact]
    public async Task Items_filter_by_tag_combined_with_search()
    {
        // Arrange
        var idMatch = await CreateItemAsync("fix bug");
        await PutTagsAsync(idMatch, ["work"]);
        var idTagOnly = await CreateItemAsync("meeting");
        await PutTagsAsync(idTagOnly, ["work"]);
        await CreateItemAsync("fix home");

        // Act
        var list = await GetAsync("/items?tag=work&q=fix");

        // Assert
        Assert.Equal(["fix bug"], Titles(list));
    }

    [Fact]
    public async Task Items_filter_by_tag_combined_with_open_and_search()
    {
        // Arrange
        var idAll = await CreateItemAsync("fix bug");
        await PutTagsAsync(idAll, ["work"]);
        var idDoneMatch = await CreateItemAsync("fix login");
        await PutTagsAsync(idDoneMatch, ["work"]);
        await MarkDoneAsync(idDoneMatch);
        var idOpenTagNoSearch = await CreateItemAsync("deploy");
        await PutTagsAsync(idOpenTagNoSearch, ["work"]);
        await CreateItemAsync("fix logout");

        // Act
        var list = await GetAsync("/items?tag=work&open=true&q=fix");

        // Assert
        Assert.Equal(["fix bug"], Titles(list));
    }

    [Fact]
    public async Task Items_filter_by_tag_ordered_newest_first()
    {
        // Arrange
        var idFirst = await CreateItemAsync("first");
        await PutTagsAsync(idFirst, ["home"]);
        var idSecond = await CreateItemAsync("second");
        await PutTagsAsync(idSecond, ["home"]);
        var idThird = await CreateItemAsync("third");
        await PutTagsAsync(idThird, ["home"]);

        // Act
        var list = await GetAsync("/items?tag=home");

        // Assert
        Assert.Equal(["third", "second", "first"], Titles(list));
    }

    [Fact]
    public async Task All_tags_sorted_by_name()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["work", "home", "urgent"]);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        var names = result
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Equal(["home", "urgent", "work"], names);
    }

    [Fact]
    public async Task All_tags_count_includes_done_items()
    {
        // Arrange
        var idOpen = await CreateItemAsync("open");
        await PutTagsAsync(idOpen, ["home"]);
        var idDone = await CreateItemAsync("done");
        await PutTagsAsync(idDone, ["home"]);
        await MarkDoneAsync(idDone);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        var entry = result
            .EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "home");
        Assert.Equal(2, entry.GetProperty("items").GetInt32());
    }

    [Fact]
    public async Task All_tags_excludes_tags_with_no_items()
    {
        // Arrange
        var id = await CreateItemAsync("task");
        await PutTagsAsync(id, ["temp"]);
        await PutTagsAsync(id, []);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        var names = result
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToArray();
        Assert.DoesNotContain("temp", names);
    }

    [Fact]
    public async Task All_tags_count_per_tag_is_correct()
    {
        // Arrange
        var id1 = await CreateItemAsync("task 1");
        await PutTagsAsync(id1, ["home", "work"]);
        var id2 = await CreateItemAsync("task 2");
        await PutTagsAsync(id2, ["home"]);

        // Act
        var result = await GetAsync("/tags");

        // Assert
        var entries = result
            .EnumerateArray()
            .ToDictionary(
                e => e.GetProperty("name").GetString()!,
                e => e.GetProperty("items").GetInt32()
            );
        Assert.Equal(2, entries["home"]);
        Assert.Equal(1, entries["work"]);
    }
}
