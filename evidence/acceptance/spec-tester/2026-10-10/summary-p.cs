using System.Net;
using System.Text.Json;

namespace Acceptance.Change20261010ItemSummary;

public sealed class ItemSummaryAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private static int OpenCount(JsonElement e) => e.GetProperty("open").GetInt32();
    private static int OverdueCount(JsonElement e) => e.GetProperty("overdue").GetInt32();
    private static int DueSoonCount(JsonElement e) => e.GetProperty("dueSoon").GetInt32();
    private static int DoneCount(JsonElement e) => e.GetProperty("done").GetInt32();
    private static int ByPriority(JsonElement e, string key) => e.GetProperty("byPriority").GetProperty(key).GetInt32();

    [Fact]
    public async Task Empty_database_returns_all_zeros()
    {
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, OpenCount(e));
        Assert.Equal(0, OverdueCount(e));
        Assert.Equal(0, DueSoonCount(e));
        Assert.Equal(0, DoneCount(e));
        foreach (var key in new[] { "1", "2", "3", "4", "5", "none" })
        {
            Assert.Equal(0, ByPriority(e, key));
        }
    }

    [Fact]
    public async Task Open_count_is_items_not_done()
    {
        await CreateItemAsync("a");
        await CreateItemAsync("b");
        await MarkDoneAsync(await CreateItemAsync("c"));
        var e = await GetAsync("/items/summary");
        Assert.Equal(2, OpenCount(e));
        Assert.Equal(1, DoneCount(e));
    }

    [Fact]
    public async Task Item_due_before_today_is_overdue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "past", dueDate = today.AddDays(-1) });
        await CreateItemAsync("no-due-date"); // must not be counted as overdue
        var e = await GetAsync("/items/summary");
        Assert.Equal(1, OverdueCount(e));
    }

    [Fact]
    public async Task Item_due_today_is_due_soon_not_overdue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "today", dueDate = today });
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, OverdueCount(e));
        Assert.Equal(1, DueSoonCount(e));
    }

    [Fact]
    public async Task Item_due_seven_days_from_today_is_due_soon()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "plus7", dueDate = today.AddDays(7) });
        await CreateItemAsync("no-due-date"); // must not count
        var e = await GetAsync("/items/summary");
        Assert.Equal(1, DueSoonCount(e));
    }

    [Fact]
    public async Task Item_due_eight_days_from_today_is_not_due_soon()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "plus8", dueDate = today.AddDays(8) });
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, DueSoonCount(e));
        Assert.Equal(0, OverdueCount(e));
    }

    [Fact]
    public async Task Item_without_due_date_is_neither_overdue_nor_due_soon()
    {
        await CreateItemAsync("no-due-date");
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, OverdueCount(e));
        Assert.Equal(0, DueSoonCount(e));
    }

    [Fact]
    public async Task Overdue_item_is_not_counted_as_due_soon()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "past", dueDate = today.AddDays(-1) });
        var e = await GetAsync("/items/summary");
        Assert.Equal(1, OverdueCount(e));
        Assert.Equal(0, DueSoonCount(e));
    }

    [Fact]
    public async Task Done_item_is_excluded_from_open_overdue_and_due_soon()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var id = (await PostAsync("/items", new { title = "done-overdue", dueDate = today.AddDays(-1) }))
            .GetProperty("id").GetInt32();
        await MarkDoneAsync(id);
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, OpenCount(e));
        Assert.Equal(0, OverdueCount(e));
        Assert.Equal(0, DueSoonCount(e));
        Assert.Equal(1, DoneCount(e));
    }

    [Fact]
    public async Task By_priority_counts_open_items_at_each_priority()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("p3", 3);
        var e = await GetAsync("/items/summary");
        Assert.Equal(1, ByPriority(e, "1"));
        Assert.Equal(1, ByPriority(e, "2"));
        Assert.Equal(1, ByPriority(e, "3"));
        Assert.Equal(0, ByPriority(e, "4"));
        Assert.Equal(0, ByPriority(e, "5"));
        Assert.Equal(0, ByPriority(e, "none"));
    }

    [Fact]
    public async Task By_priority_none_counts_open_items_without_priority()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("no-priority");
        var e = await GetAsync("/items/summary");
        Assert.Equal(1, ByPriority(e, "1"));
        Assert.Equal(1, ByPriority(e, "none"));
    }

    [Fact]
    public async Task Every_priority_key_is_present_even_when_zero()
    {
        await CreateItemAsync("p3-only", 3);
        var bp = (await GetAsync("/items/summary")).GetProperty("byPriority");
        foreach (var key in new[] { "1", "2", "3", "4", "5", "none" })
        {
            Assert.True(bp.TryGetProperty(key, out _), $"byPriority is missing key '{key}'");
        }
    }

    [Fact]
    public async Task By_priority_excludes_done_items()
    {
        await MarkDoneAsync(await CreateItemAsync("done-p1", 1));
        var e = await GetAsync("/items/summary");
        Assert.Equal(0, ByPriority(e, "1"));
    }

    [Fact]
    public async Task Search_narrows_open_count()
    {
        await CreateItemAsync("invoice alpha");
        await CreateItemAsync("groceries"); // must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, OpenCount(e));
    }

    [Fact]
    public async Task Search_narrows_done_count()
    {
        await MarkDoneAsync(await CreateItemAsync("invoice done"));
        await MarkDoneAsync(await CreateItemAsync("groceries done")); // must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, DoneCount(e));
    }

    [Fact]
    public async Task Search_narrows_overdue_count()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "invoice overdue", dueDate = today.AddDays(-1) });
        await PostAsync("/items", new { title = "groceries overdue", dueDate = today.AddDays(-1) }); // must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, OverdueCount(e));
    }

    [Fact]
    public async Task Search_narrows_due_soon_count()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostAsync("/items", new { title = "invoice soon", dueDate = today.AddDays(3) });
        await PostAsync("/items", new { title = "groceries soon", dueDate = today.AddDays(3) }); // must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, DueSoonCount(e));
    }

    [Fact]
    public async Task Search_narrows_by_priority()
    {
        await CreateItemAsync("invoice p1", 1);
        await CreateItemAsync("groceries p1", 1); // same priority, different title — must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, ByPriority(e, "1"));
        Assert.Equal(0, ByPriority(e, "2"));
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await CreateItemAsync("Invoice");
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, OpenCount(e));
    }

    [Fact]
    public async Task Search_matches_note_field()
    {
        await PostAsync("/items", new { title = "Alpha", note = "contains invoice detail" });
        await CreateItemAsync("Beta"); // title and note don't match — must not count
        var e = await GetAsync("/items/summary?q=invoice");
        Assert.Equal(1, OpenCount(e));
    }

    [Fact]
    public async Task Empty_q_means_no_narrowing()
    {
        await CreateItemAsync("a");
        await CreateItemAsync("b");
        var e = await GetAsync("/items/summary?q=");
        Assert.Equal(2, OpenCount(e));
    }

    [Fact]
    public async Task Whitespace_q_means_no_narrowing()
    {
        await CreateItemAsync("a");
        await CreateItemAsync("b");
        var e = await GetAsync("/items/summary?q=" + Uri.EscapeDataString("   "));
        Assert.Equal(2, OpenCount(e));
    }

    [Fact]
    public async Task Q_of_exactly_100_characters_is_accepted()
    {
        await GetAsync("/items/summary?q=" + new string('x', 100));
    }

    [Fact]
    public async Task Q_longer_than_100_characters_returns_400()
    {
        await GetAsync("/items/summary?q=" + new string('x', 101), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Summary_does_not_modify_any_item()
    {
        await CreateItemAsync("alpha");
        await CreateItemAsync("beta");
        await GetAsync("/items/summary");
        var items = await GetAsync("/items");
        Assert.Equal(2, items.GetArrayLength());
    }
}
