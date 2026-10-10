// Brief: briefs/item-upcoming.md. Written before the run, from the brief only.
using System.Net;
namespace Acceptance;

public sealed class ItemUpcomingAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private static string Day(int offset) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset).ToString("yyyy-MM-dd");

    private async Task<int> Due(string title, int? offset) =>
        (await PostAsync("/items", new { title, dueDate = offset is null ? null : Day(offset.Value) }))
            .GetProperty("id").GetInt32();

    [Fact]
    public async Task Default_is_today_through_seven_days_inclusive_soonest_first()
    {
        await Due("d7", 7);
        await Due("d8", 8);
        await Due("d0", 0);
        await Due("overdue", -1);
        await Due("none", null);
        await Due("d3", 3);
        Assert.Equal(["d0", "d3", "d7"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Days_parameter_bounds_the_window_inclusively()
    {
        await Due("d2", 2);
        await Due("d3", 3);
        await Due("d1", 1);
        Assert.Equal(["d1", "d2"], Titles(await GetAsync("/items/upcoming?days=2")));
    }

    [Fact]
    public async Task Zero_days_is_today_only()
    {
        await Due("d1", 1);
        await Due("d0", 0);
        Assert.Equal(["d0"], Titles(await GetAsync("/items/upcoming?days=0")));
    }

    [Fact]
    public async Task Same_day_ties_are_added_first_first()
    {
        await Due("second-day-a", 2);
        await Due("first-day", 1);
        await Due("second-day-b", 2);
        Assert.Equal(["first-day", "second-day-a", "second-day-b"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Done_items_are_excluded()
    {
        await Due("open", 1);
        await MarkDoneAsync(await Due("done", 1));
        Assert.Equal(["open"], Titles(await GetAsync("/items/upcoming")));
    }

    [Fact]
    public async Task Upper_bound_365_is_allowed()
    {
        await Due("far", 365);
        await Due("too-far", 366);
        Assert.Equal(["far"], Titles(await GetAsync("/items/upcoming?days=365")));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("366")]
    [InlineData("abc")]
    public async Task Out_of_range_or_non_numeric_days_is_400(string days) =>
        await GetAsync($"/items/upcoming?days={days}", HttpStatusCode.BadRequest);

    [Fact]
    public async Task Items_have_the_list_shape()
    {
        await PostAsync("/items", new { title = "x", note = "n", priority = 2, dueDate = Day(1) });
        var item = (await GetAsync("/items/upcoming"))[0];
        Assert.Equal("n", item.GetProperty("note").GetString());
        Assert.Equal(2, item.GetProperty("priority").GetInt32());
        Assert.Equal(Day(1), item.GetProperty("dueDate").GetString());
    }
}
