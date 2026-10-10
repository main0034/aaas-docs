using System.Net;

namespace Acceptance;

public sealed class SummaryAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<System.Text.Json.JsonElement> GetSummaryAsync(string? q = null)
    {
        var url = q is null ? "/items/summary" : $"/items/summary?q={Uri.EscapeDataString(q)}";
        return await GetAsync(url);
    }

    [Fact]
    public async Task Empty_state_returns_all_zeros_with_all_six_priority_keys()
    {
        // Arrange

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("open").GetInt32());
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
        Assert.Equal(0, summary.GetProperty("done").GetInt32());
        var bp = summary.GetProperty("byPriority");
        Assert.Equal(0, bp.GetProperty("1").GetInt32());
        Assert.Equal(0, bp.GetProperty("2").GetInt32());
        Assert.Equal(0, bp.GetProperty("3").GetInt32());
        Assert.Equal(0, bp.GetProperty("4").GetInt32());
        Assert.Equal(0, bp.GetProperty("5").GetInt32());
        Assert.Equal(0, bp.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task Open_counts_items_not_done_and_done_counts_items_marked_done()
    {
        // Arrange
        await CreateItemAsync("open-a");
        await CreateItemAsync("open-b");
        await MarkDoneAsync(await CreateItemAsync("done-a"));
        await MarkDoneAsync(await CreateItemAsync("done-b"));
        await MarkDoneAsync(await CreateItemAsync("done-c"));

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(2, summary.GetProperty("open").GetInt32());
        Assert.Equal(3, summary.GetProperty("done").GetInt32());
    }

    [Fact]
    public async Task Overdue_is_open_item_with_due_date_before_today()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await PostAsync("/items", new { title = "past-due", dueDate = yesterday });

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
    }

    [Fact]
    public async Task Overdue_item_is_not_also_counted_as_due_soon()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await PostAsync("/items", new { title = "past-due", dueDate = yesterday });

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Done_item_with_past_due_date_is_not_counted_as_overdue()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var id = (await PostAsync("/items", new { title = "done-past-due", dueDate = yesterday }))
            .GetProperty("id").GetInt32();
        await MarkDoneAsync(id);

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
    }

    [Fact]
    public async Task Due_soon_includes_today()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "due-today", dueDate = today });

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Due_soon_includes_today_plus_7()
    {
        // Arrange
        var todayPlus7 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        await PostAsync("/items", new { title = "due-plus7", dueDate = todayPlus7 });

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Due_soon_excludes_today_plus_8()
    {
        // Arrange
        var todayPlus8 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(8);
        await PostAsync("/items", new { title = "due-plus8", dueDate = todayPlus8 });

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Item_without_due_date_is_neither_overdue_nor_due_soon()
    {
        // Arrange
        await CreateItemAsync("no-due-date");

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task By_priority_counts_open_items_at_each_priority_and_none()
    {
        // Arrange
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("p3", 3);
        await CreateItemAsync("p4", 4);
        await CreateItemAsync("p5", 5);
        await CreateItemAsync("no-priority");

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        var bp = summary.GetProperty("byPriority");
        Assert.Equal(1, bp.GetProperty("1").GetInt32());
        Assert.Equal(1, bp.GetProperty("2").GetInt32());
        Assert.Equal(1, bp.GetProperty("3").GetInt32());
        Assert.Equal(1, bp.GetProperty("4").GetInt32());
        Assert.Equal(1, bp.GetProperty("5").GetInt32());
        Assert.Equal(1, bp.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task By_priority_excludes_done_items()
    {
        // Arrange
        await CreateItemAsync("open-p1", 1);
        await MarkDoneAsync(await CreateItemAsync("done-p1", 1));

        // Act
        var summary = await GetSummaryAsync();

        // Assert
        Assert.Equal(1, summary.GetProperty("byPriority").GetProperty("1").GetInt32());
    }

    [Fact]
    public async Task Search_narrows_open_done_overdue_due_soon_and_by_priority()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        await PostAsync("/items", new { title = "apple-open", priority = 1 });
        await MarkDoneAsync(
            (await PostAsync("/items", new { title = "apple-done" })).GetProperty("id").GetInt32());
        await PostAsync("/items", new { title = "apple-overdue", dueDate = yesterday, priority = 2 });
        await PostAsync("/items", new { title = "apple-soon", dueDate = today, priority = 3 });
        await CreateItemAsync("banana-open");

        // Act
        var summary = await GetSummaryAsync("apple");

        // Assert
        Assert.Equal(3, summary.GetProperty("open").GetInt32());
        Assert.Equal(1, summary.GetProperty("done").GetInt32());
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
        var bp = summary.GetProperty("byPriority");
        Assert.Equal(1, bp.GetProperty("1").GetInt32());
        Assert.Equal(1, bp.GetProperty("2").GetInt32());
        Assert.Equal(1, bp.GetProperty("3").GetInt32());
        Assert.Equal(0, bp.GetProperty("4").GetInt32());
        Assert.Equal(0, bp.GetProperty("5").GetInt32());
        Assert.Equal(0, bp.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task Search_matches_title_case_insensitively()
    {
        // Arrange
        await CreateItemAsync("UPPERCASE");
        await CreateItemAsync("other");

        // Act
        var summary = await GetSummaryAsync("uppercase");

        // Assert
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Search_matches_note_case_insensitively()
    {
        // Arrange
        await PostAsync("/items", new { title = "note-item", note = "UPPERCASE NOTE" });
        await CreateItemAsync("other");

        // Act
        var summary = await GetSummaryAsync("uppercase");

        // Assert
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Search_whitespace_only_means_no_narrowing()
    {
        // Arrange
        await CreateItemAsync("first");
        await CreateItemAsync("second");

        // Act
        var summary = await GetSummaryAsync("   ");

        // Assert
        Assert.Equal(2, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Search_q_longer_than_100_characters_returns_400()
    {
        // Act & Assert
        await GetAsync($"/items/summary?q={new string('a', 101)}", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_q_exactly_100_characters_is_accepted()
    {
        // Act & Assert
        await GetAsync($"/items/summary?q={new string('a', 100)}", HttpStatusCode.OK);
    }

    [Fact]
    public async Task Summary_does_not_modify_items()
    {
        // Arrange
        await CreateItemAsync("item-a");
        await CreateItemAsync("item-b");
        await GetSummaryAsync();

        // Act
        var items = await GetAsync("/items");

        // Assert
        Assert.Equal(2, items.GetArrayLength());
    }
}
