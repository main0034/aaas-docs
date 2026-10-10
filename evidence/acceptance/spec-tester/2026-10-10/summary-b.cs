using System.Net;

namespace Acceptance;

public sealed class SummaryAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    // ── Basic shape ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Empty_database_returns_all_zeros()
    {
        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(0, summary.GetProperty("open").GetInt32());
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
        Assert.Equal(0, summary.GetProperty("done").GetInt32());
        var byP = summary.GetProperty("byPriority");
        Assert.Equal(0, byP.GetProperty("1").GetInt32());
        Assert.Equal(0, byP.GetProperty("2").GetInt32());
        Assert.Equal(0, byP.GetProperty("3").GetInt32());
        Assert.Equal(0, byP.GetProperty("4").GetInt32());
        Assert.Equal(0, byP.GetProperty("5").GetInt32());
        Assert.Equal(0, byP.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task Open_count_excludes_done_items()
    {
        // Arrange
        await CreateItemAsync("open-a");
        await CreateItemAsync("open-b");
        await CreateItemAsync("open-c");
        await MarkDoneAsync(await CreateItemAsync("done-x"));
        await MarkDoneAsync(await CreateItemAsync("done-y"));

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(3, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Done_count_is_count_of_marked_done_items()
    {
        // Arrange
        await CreateItemAsync("open");
        await MarkDoneAsync(await CreateItemAsync("done-a"));
        await MarkDoneAsync(await CreateItemAsync("done-b"));

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(2, summary.GetProperty("done").GetInt32());
    }

    // ── Overdue boundaries ─────────────────────────────────────────────────────

    [Fact]
    public async Task Item_due_yesterday_is_overdue_not_due_soon()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "yesterday", dueDate = today.AddDays(-1) });

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Item_due_today_is_due_soon_not_overdue()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "today", dueDate = today });

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
    }

    // ── Due soon boundaries ────────────────────────────────────────────────────

    [Fact]
    public async Task Item_due_today_plus_7_is_due_soon()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "last-day", dueDate = today.AddDays(7) });

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Item_due_today_plus_8_is_not_due_soon()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "just-outside", dueDate = today.AddDays(8) });

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
    }

    // ── No due date / done items with dates ────────────────────────────────────

    [Fact]
    public async Task Item_without_due_date_is_neither_overdue_nor_due_soon()
    {
        // Arrange
        await CreateItemAsync("no-date");

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Done_items_are_not_counted_in_overdue_or_due_soon()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await MarkDoneAsync(
            (await PostAsync("/items", new { title = "done-overdue", dueDate = today.AddDays(-1) }))
                .GetProperty("id")
                .GetInt32());
        await MarkDoneAsync(
            (await PostAsync("/items", new { title = "done-due-soon", dueDate = today }))
                .GetProperty("id")
                .GetInt32());

        // Act
        var summary = await GetAsync("/items/summary");

        // Assert
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(0, summary.GetProperty("dueSoon").GetInt32());
    }

    // ── byPriority ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task By_priority_all_five_levels_and_none_appear_even_with_zero_count()
    {
        // Arrange
        await CreateItemAsync("p3-only", 3);

        // Act
        var byP = (await GetAsync("/items/summary")).GetProperty("byPriority");

        // Assert
        Assert.Equal(0, byP.GetProperty("1").GetInt32());
        Assert.Equal(0, byP.GetProperty("2").GetInt32());
        Assert.Equal(1, byP.GetProperty("3").GetInt32());
        Assert.Equal(0, byP.GetProperty("4").GetInt32());
        Assert.Equal(0, byP.GetProperty("5").GetInt32());
        Assert.Equal(0, byP.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task By_priority_counts_open_items_at_each_level()
    {
        // Arrange
        await CreateItemAsync("p1a", 1);
        await CreateItemAsync("p1b", 1);
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("p3", 3);
        await CreateItemAsync("p4", 4);
        await CreateItemAsync("p5", 5);

        // Act
        var byP = (await GetAsync("/items/summary")).GetProperty("byPriority");

        // Assert
        Assert.Equal(2, byP.GetProperty("1").GetInt32());
        Assert.Equal(1, byP.GetProperty("2").GetInt32());
        Assert.Equal(1, byP.GetProperty("3").GetInt32());
        Assert.Equal(1, byP.GetProperty("4").GetInt32());
        Assert.Equal(1, byP.GetProperty("5").GetInt32());
        Assert.Equal(0, byP.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task By_priority_none_counts_open_items_without_a_priority()
    {
        // Arrange
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("no-pri-a");
        await CreateItemAsync("no-pri-b");

        // Act
        var byP = (await GetAsync("/items/summary")).GetProperty("byPriority");

        // Assert
        Assert.Equal(1, byP.GetProperty("1").GetInt32());
        Assert.Equal(2, byP.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task By_priority_excludes_done_items()
    {
        // Arrange
        await CreateItemAsync("open-p1", 1);
        await MarkDoneAsync(await CreateItemAsync("done-p1", 1));

        // Act
        var byP = (await GetAsync("/items/summary")).GetProperty("byPriority");

        // Assert
        Assert.Equal(1, byP.GetProperty("1").GetInt32());
    }

    // ── Search ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_narrows_open_and_done_counts()
    {
        // Arrange
        await CreateItemAsync("apple open");
        await CreateItemAsync("banana open");
        await MarkDoneAsync(await CreateItemAsync("apple done"));
        await MarkDoneAsync(await CreateItemAsync("banana done"));

        // Act
        var summary = await GetAsync("/items/summary?q=apple");

        // Assert
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
        Assert.Equal(1, summary.GetProperty("done").GetInt32());
    }

    [Fact]
    public async Task Search_narrows_overdue_and_due_soon()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "apple overdue", dueDate = today.AddDays(-1) });
        await PostAsync("/items", new { title = "banana overdue", dueDate = today.AddDays(-1) });
        await PostAsync("/items", new { title = "apple due-soon", dueDate = today });
        await PostAsync("/items", new { title = "banana due-soon", dueDate = today });

        // Act
        var summary = await GetAsync("/items/summary?q=apple");

        // Assert
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueSoon").GetInt32());
    }

    [Fact]
    public async Task Search_narrows_by_priority_counts()
    {
        // Arrange
        await CreateItemAsync("apple p1", 1);
        await CreateItemAsync("banana p1", 1);
        await CreateItemAsync("apple p2", 2);
        await CreateItemAsync("apple no-pri");

        // Act
        var byP = (await GetAsync("/items/summary?q=apple")).GetProperty("byPriority");

        // Assert
        Assert.Equal(1, byP.GetProperty("1").GetInt32());
        Assert.Equal(1, byP.GetProperty("2").GetInt32());
        Assert.Equal(0, byP.GetProperty("3").GetInt32());
        Assert.Equal(0, byP.GetProperty("4").GetInt32());
        Assert.Equal(0, byP.GetProperty("5").GetInt32());
        Assert.Equal(1, byP.GetProperty("none").GetInt32());
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        // Arrange
        await CreateItemAsync("Apple Open");
        await CreateItemAsync("other");

        // Act
        var summary = await GetAsync("/items/summary?q=apple");

        // Assert
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Search_matches_note_field()
    {
        // Arrange
        await PostAsync("/items", new { title = "no-match-in-title", note = "apple note" });
        await CreateItemAsync("unrelated");

        // Act
        var summary = await GetAsync("/items/summary?q=apple");

        // Assert
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Empty_q_means_no_filtering()
    {
        // Arrange
        await CreateItemAsync("item-a");
        await CreateItemAsync("item-b");

        // Act
        var summary = await GetAsync("/items/summary?q=");

        // Assert
        Assert.Equal(2, summary.GetProperty("open").GetInt32());
    }

    [Fact]
    public async Task Whitespace_q_means_no_filtering()
    {
        // Arrange
        await CreateItemAsync("item-a");
        await CreateItemAsync("item-b");

        // Act
        var summary = await GetAsync("/items/summary?q=%20%20");

        // Assert
        Assert.Equal(2, summary.GetProperty("open").GetInt32());
    }

    // ── q length validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Q_exactly_100_chars_is_accepted()
    {
        // Act & Assert
        await GetAsync("/items/summary?q=" + new string('a', 100));
    }

    [Fact]
    public async Task Q_longer_than_100_chars_returns_400()
    {
        // Act & Assert
        await GetAsync("/items/summary?q=" + new string('a', 101), HttpStatusCode.BadRequest);
    }

    // ── Read-only ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_does_not_create_or_modify_items()
    {
        // Arrange
        await CreateItemAsync("existing");
        var before = await GetAsync("/items");

        // Act
        await GetAsync("/items/summary");

        // Assert
        var after = await GetAsync("/items");
        Assert.Equal(Titles(before), Titles(after));
    }
}
