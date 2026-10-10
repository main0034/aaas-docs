// Calibration. Brief: briefs/item-priority.md. The brief has no interface line, so the
// route and parameter were read from PR #13: GET /items/priority?maxPriority=N.
namespace Acceptance;

public sealed class ItemPriorityAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    [Fact]
    public async Task Orders_one_to_five_then_none_newest_first_within_ties()
    {
        await CreateItemAsync("p3", 3);
        await CreateItemAsync("none-a");
        await CreateItemAsync("p1-a", 1);
        await CreateItemAsync("p5", 5);
        await CreateItemAsync("p1-b", 1);
        await CreateItemAsync("none-b");
        await CreateItemAsync("p2", 2);
        Assert.Equal(["p1-b", "p1-a", "p2", "p3", "p5", "none-b", "none-a"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Done_items_are_not_listed()
    {
        await CreateItemAsync("keep", 2);
        await MarkDoneAsync(await CreateItemAsync("gone", 1));
        await MarkDoneAsync(await CreateItemAsync("gone-none"));
        Assert.Equal(["keep"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Limit_two_gives_only_priorities_one_and_two()
    {
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("p3", 3);
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("none");
        Assert.Equal(["p1", "p2"], Titles(await GetAsync("/items/priority?maxPriority=2")));
    }

    [Fact]
    public async Task Limit_five_gives_every_prioritised_item_but_not_unprioritised()
    {
        await CreateItemAsync("p5", 5);
        await CreateItemAsync("none");
        await CreateItemAsync("p4", 4);
        Assert.Equal(["p4", "p5"], Titles(await GetAsync("/items/priority?maxPriority=5")));
    }
}
