namespace Acceptance;

public sealed class PriorityListAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    [Fact]
    public async Task Priority_1_comes_before_higher_numbers()
    {
        // Create p1 first (older/lower id) so a naive newest-first sort would put p2 ahead.
        await CreateItemAsync("first", 1);
        await CreateItemAsync("second", 2);
        Assert.Equal(["first", "second"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task All_five_priorities_ordered_one_through_five()
    {
        await CreateItemAsync("p5", 5);
        await CreateItemAsync("p3", 3);
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p4", 4);
        await CreateItemAsync("p2", 2);
        Assert.Equal(["p1", "p2", "p3", "p4", "p5"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Items_without_priority_come_after_all_prioritised_items()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("no-priority"); // newest (highest id) — must still be last
        Assert.Equal(["p1", "p2", "no-priority"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Same_priority_items_are_newest_first()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2-older", 2); // lower id
        await CreateItemAsync("p2-newer", 2); // higher id — must come before p2-older
        Assert.Equal(["p1", "p2-newer", "p2-older"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Unprioritised_items_are_newest_first_among_themselves()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("none-older"); // lower id
        await CreateItemAsync("none-newer"); // higher id — must come before none-older
        Assert.Equal(["p1", "none-newer", "none-older"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task Done_items_are_not_listed()
    {
        await CreateItemAsync("keep", 2);
        await MarkDoneAsync(await CreateItemAsync("gone", 1)); // priority 1 — would be first if open
        Assert.Equal(["keep"], Titles(await GetAsync("/items/priority")));
    }

    [Fact]
    public async Task MaxPriority_filter_includes_boundary_and_excludes_beyond()
    {
        // maxPriority=2 means "priority 2 or more important" → only 1 and 2
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2", 2);
        await CreateItemAsync("p3", 3); // must be excluded
        Assert.Equal(["p1", "p2"], Titles(await GetAsync("/items/priority?maxPriority=2")));
    }

    [Fact]
    public async Task MaxPriority_1_returns_only_priority_1()
    {
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("p2", 2); // must be excluded
        Assert.Equal(["p1"], Titles(await GetAsync("/items/priority?maxPriority=1")));
    }

    [Fact]
    public async Task MaxPriority_filter_excludes_items_without_priority()
    {
        // "priority 2 or more important gives only priorities 1 and 2" — no-priority is neither
        await CreateItemAsync("p1", 1);
        await CreateItemAsync("no-priority"); // must be excluded even though it appears unfiltered
        Assert.Equal(["p1"], Titles(await GetAsync("/items/priority?maxPriority=2")));
    }
}
