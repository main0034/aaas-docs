using System.Net;

namespace Acceptance;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<int> CreateProjectAsync(string name)
    {
        var project = await PostAsync("/projects", new { name });
        return project.GetProperty("id").GetInt32();
    }

    // POST /projects

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        // Act
        var project = await PostAsync("/projects", new { name = "Garden" });

        // Assert
        Assert.True(project.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Garden", project.GetProperty("name").GetString()!);
    }

    [Fact]
    public async Task Name_of_one_character_is_accepted()
    {
        // Act & Assert
        await PostAsync("/projects", new { name = "A" });
    }

    [Fact]
    public async Task Empty_name_is_rejected()
    {
        // Act & Assert
        await PostAsync("/projects", new { name = "" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Name_of_100_characters_is_accepted()
    {
        // Arrange
        var name = new string('x', 100);

        // Act & Assert
        await PostAsync("/projects", new { name });
    }

    [Fact]
    public async Task Name_of_101_characters_is_rejected()
    {
        // Arrange
        var name = new string('x', 101);

        // Act & Assert
        await PostAsync("/projects", new { name }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Whitespace_only_name_is_rejected()
    {
        // Act & Assert
        await PostAsync("/projects", new { name = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Name_trimmed_to_100_characters_is_accepted()
    {
        // Arrange — raw length 104, trimmed length 100: valid only after trimming
        var name = "  " + new string('x', 100) + "  ";

        // Act & Assert
        await PostAsync("/projects", new { name });
    }

    [Fact]
    public async Task Name_trimmed_to_101_characters_is_rejected()
    {
        // Arrange — raw length 105, trimmed length 101: invalid after trimming
        var name = "  " + new string('x', 101) + "  ";

        // Act & Assert
        await PostAsync("/projects", new { name }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Name_is_stored_trimmed()
    {
        // Act
        var project = await PostAsync("/projects", new { name = "  Garden  " });

        // Assert
        Assert.Equal("Garden", project.GetProperty("name").GetString()!);
    }

    [Fact]
    public async Task Duplicate_name_same_case_returns_409()
    {
        // Arrange
        await PostAsync("/projects", new { name = "Garden" });

        // Act & Assert
        await PostAsync("/projects", new { name = "Garden" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Duplicate_name_different_case_returns_409()
    {
        // Arrange
        await PostAsync("/projects", new { name = "Garden" });

        // Act & Assert
        await PostAsync("/projects", new { name = "GARDEN" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Name_taken_check_uses_trimmed_value()
    {
        // Arrange — "  garden  " is stored as "garden"; "GARDEN" conflicts with it
        await PostAsync("/projects", new { name = "  garden  " });

        // Act & Assert
        await PostAsync("/projects", new { name = "GARDEN" }, HttpStatusCode.Conflict);
    }

    // PUT /items/{id}/project

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        // Arrange
        var itemId = await CreateItemAsync("task");
        var projectId = await CreateProjectAsync("Work");

        // Act
        var item = await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId },
            HttpStatusCode.OK
        );

        // Assert
        Assert.Equal(itemId, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Remove_item_from_project_returns_200()
    {
        // Arrange
        var itemId = await CreateItemAsync("task");
        var projectId = await CreateProjectAsync("Work");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act & Assert
        await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId = (int?)null },
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task Removed_item_no_longer_counted_in_project()
    {
        // Arrange
        var itemId = await CreateItemAsync("task");
        var projectId = await CreateProjectAsync("Work");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId = (int?)null },
            HttpStatusCode.OK
        );

        // Assert
        var project = await GetAsync($"/projects/{projectId}");
        Assert.Equal(0, project.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Item_moved_to_another_project_appears_only_in_new_project()
    {
        // Arrange
        var itemId = await CreateItemAsync("task");
        var p1Id = await CreateProjectAsync("Alpha");
        var p2Id = await CreateProjectAsync("Beta");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = p1Id }, HttpStatusCode.OK);

        // Act
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = p2Id }, HttpStatusCode.OK);

        // Assert
        var p1 = await GetAsync($"/projects/{p1Id}");
        var p2 = await GetAsync($"/projects/{p2Id}");
        Assert.Equal(0, p1.GetProperty("total").GetInt32());
        Assert.Equal(1, p2.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Assign_unknown_item_returns_404()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Work");

        // Act & Assert
        await SendAsync(HttpMethod.Put, "/items/99999/project", new { projectId }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_to_unknown_project_returns_400()
    {
        // Arrange
        var itemId = await CreateItemAsync("task");

        // Act & Assert
        await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId = 99999 },
            HttpStatusCode.BadRequest
        );
    }

    // GET /projects

    [Fact]
    public async Task Projects_are_sorted_by_name_ignoring_case()
    {
        // Arrange — 'B' > 'a' in ASCII but 'b' > 'a' and 'b' < 'c' ignoring case
        await PostAsync("/projects", new { name = "Cherry" });
        await PostAsync("/projects", new { name = "alpha" });
        await PostAsync("/projects", new { name = "Blue" });

        // Act
        var list = await GetAsync("/projects");

        // Assert
        var names = list.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(["alpha", "Blue", "Cherry"], names);
    }

    [Fact]
    public async Task Project_total_and_done_counts_are_correct()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("a");
        var item2 = await CreateItemAsync("b");
        var item3 = await CreateItemAsync("c");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item3}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(item1);

        // Act
        var list = await GetAsync("/projects");
        var project = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == projectId);

        // Assert
        Assert.Equal(3, project.GetProperty("total").GetInt32());
        Assert.Equal(1, project.GetProperty("done").GetInt32());
    }

    [Fact]
    public async Task Percent_done_is_rounded_down()
    {
        // Arrange — 2 of 3 done: floor(66.67) = 66, not 67
        var projectId = await CreateProjectAsync("Work");
        var item1 = await CreateItemAsync("a");
        var item2 = await CreateItemAsync("b");
        var item3 = await CreateItemAsync("c");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item3}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(item1);
        await MarkDoneAsync(item2);

        // Act
        var list = await GetAsync("/projects");
        var project = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == projectId);

        // Assert
        Assert.Equal(66, project.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Project_with_no_items_is_0_percent_done()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Empty");

        // Act
        var list = await GetAsync("/projects");
        var project = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == projectId);

        // Assert
        Assert.Equal(0, project.GetProperty("total").GetInt32());
        Assert.Equal(0, project.GetProperty("done").GetInt32());
        Assert.Equal(0, project.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Items_from_other_projects_are_not_counted()
    {
        // Arrange
        var p1Id = await CreateProjectAsync("Alpha");
        var p2Id = await CreateProjectAsync("Beta");
        var item1 = await CreateItemAsync("a");
        var item2 = await CreateItemAsync("b");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId = p1Id }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId = p2Id }, HttpStatusCode.OK);

        // Act
        var list = await GetAsync("/projects");
        var p1 = list.EnumerateArray().First(p => p.GetProperty("id").GetInt32() == p1Id);

        // Assert
        Assert.Equal(1, p1.GetProperty("total").GetInt32());
    }

    // GET /projects/{id}

    [Fact]
    public async Task Get_project_by_id_returns_project_fields_and_items()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var itemId = await CreateItemAsync("Plant roses");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        var project = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal("Garden", project.GetProperty("name").GetString()!);
        Assert.Equal(1, project.GetProperty("total").GetInt32());
        Assert.Equal(["Plant roses"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Project_page_lists_open_items_before_done_items()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Work");
        var openId = await CreateItemAsync("open");
        var doneId = await CreateItemAsync("done"); // higher id — would be first in a naive newest-first sort
        await SendAsync(HttpMethod.Put, $"/items/{openId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{doneId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(doneId);

        // Act
        var project = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["open", "done"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Open_items_within_project_are_newest_first()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Work");
        var olderId = await CreateItemAsync("older-open");
        var newerId = await CreateItemAsync("newer-open"); // higher id, added later
        await SendAsync(HttpMethod.Put, $"/items/{olderId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{newerId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        var project = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["newer-open", "older-open"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Done_items_within_project_are_newest_first()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Work");
        var olderDoneId = await CreateItemAsync("older-done");
        var newerDoneId = await CreateItemAsync("newer-done"); // higher id, added later
        var openId = await CreateItemAsync("open");
        await SendAsync(HttpMethod.Put, $"/items/{olderDoneId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{newerDoneId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{openId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(olderDoneId);
        await MarkDoneAsync(newerDoneId);

        // Act
        var project = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["open", "newer-done", "older-done"], Titles(project.GetProperty("items")));
    }

    [Fact]
    public async Task Get_unknown_project_returns_404()
    {
        // Act & Assert
        await GetAsync("/projects/99999", HttpStatusCode.NotFound);
    }

    // DELETE /projects/{id}

    [Fact]
    public async Task Delete_empty_project_returns_204()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Temp");

        // Act & Assert
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        // Arrange
        var projectId = await CreateProjectAsync("NonEmpty");
        var itemId = await CreateItemAsync("task");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act & Assert
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_project_with_only_done_items_still_returns_409()
    {
        // Arrange — "no items" means all items, not only open ones
        var projectId = await CreateProjectAsync("DoneOnly");
        var itemId = await CreateItemAsync("done task");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(itemId);

        // Act & Assert
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_unknown_project_returns_404()
    {
        // Act & Assert
        await SendAsync(HttpMethod.Delete, "/projects/99999", null, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task After_failed_delete_project_still_exists()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Stay");
        var itemId = await CreateItemAsync("task");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.Conflict);

        // Assert
        await GetAsync($"/projects/{projectId}");
    }
}
