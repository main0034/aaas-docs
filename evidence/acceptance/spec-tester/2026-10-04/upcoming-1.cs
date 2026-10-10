using System.Net;

namespace Acceptance;

public sealed class UpcomingAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<int> CreateWithDueDateAsync(string title, DateOnly? dueDate) =>
        (await PostAsync("/items", new { title, dueDate })).GetProperty("id").GetInt32();

    [Fact]
    public async Task Items_due_today_are_included()
    {
        var today = Today;
        await CreateWithDueDateAsync("today-item", today);
        await CreateWithDueDateAsync("no-date-item", null);
        Assert.Equal(["today-item"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Overdue_items_are_excluded()
    {
        var today = Today;
        await CreateWithDueDateAsync("overdue", today.AddDays(-1));
        await CreateWithDueDateAsync("today", today);
        Assert.Equal(["today"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Done_items_are_excluded()
    {
        var today = Today;
        int doneId = await CreateWithDueDateAsync("done-item", today);
        await MarkDoneAsync(doneId);
        await CreateWithDueDateAsync("open-item", today.AddDays(1));
        Assert.Equal(["open-item"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Last_day_of_window_is_included()
    {
        var today = Today;
        await CreateWithDueDateAsync("last-day", today.AddDays(7));
        await CreateWithDueDateAsync("past-window", today.AddDays(8));
        Assert.Equal(["last-day"], Titles(await GetAsync("/items/upcoming?days=7")));
    }

    [Fact]
    public async Task Default_days_is_seven()
    {
        var today = Today;
        await CreateWithDueDateAsync("day-7", today.AddDays(7));
        await CreateWithDueDateAsync("day-8", today.AddDays(8));
        Assert.Equal(["day-7"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Days_zero_shows_only_today()
    {
        var today = Today;
        await CreateWithDueDateAsync("today", today);
        await CreateWithDueDateAsync("tomorrow", today.AddDays(1));
        Assert.Equal(["today"], Titles(await GetAsync("/items/upcoming?days=0")));
    }

    [Fact]
    public async Task Days_365_is_accepted()
    {
        await GetAsync("/items/upcoming?days=365");
    }

    [Fact]
    public async Task Days_minus_one_is_refused()
    {
        await GetAsync("/items/upcoming?days=-1", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Days_366_is_refused()
    {
        await GetAsync("/items/upcoming?days=366", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_numeric_days_is_refused()
    {
        await GetAsync("/items/upcoming?days=abc", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Items_ordered_soonest_first()
    {
        var today = Today;
        await CreateWithDueDateAsync("c", today.AddDays(3));
        await CreateWithDueDateAsync("a", today.AddDays(1));
        await CreateWithDueDateAsync("b", today.AddDays(2));
        Assert.Equal(["a", "b", "c"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Same_day_items_ordered_by_insertion_order()
    {
        var today = Today;
        // "first" is inserted before "second" (lower id); "third" is between them in insertion
        // order but has a later due date — proves the sort key is date then id, not just id.
        await CreateWithDueDateAsync("first", today.AddDays(1));
        await CreateWithDueDateAsync("third", today.AddDays(2));
        await CreateWithDueDateAsync("second", today.AddDays(1));
        Assert.Equal(["first", "second", "third"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task All_exclusions_apply_together()
    {
        var today = Today;
        int doneId = await CreateWithDueDateAsync("done", today.AddDays(1));
        await MarkDoneAsync(doneId);
        await CreateWithDueDateAsync("overdue", today.AddDays(-1));
        await CreateWithDueDateAsync("no-date", null);
        await CreateWithDueDateAsync("open-upcoming", today.AddDays(1));
        Assert.Equal(["open-upcoming"], Titles(await GetAsync("/items/upcoming")));
    }
}
