using System.Net;

namespace Acceptance;

public sealed class ProjectsAcceptance(App.Tests.Postgres.TemplateDatabase t) : AcceptanceBase(t)
{
    private async Task<int> CreateProjectAsync(string name)
    {
        var result = await PostAsync("/projects", new { name });
        return result.GetProperty("id").GetInt32();
    }

    // POST /projects

    [Fact]
    public async Task Create_project_returns_201_with_id_and_name()
    {
        // Act
        var result = await PostAsync("/projects", new { name = "Garden" });

        // Assert
        Assert.True(result.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Garden", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_empty_name_is_invalid()
    {
        // Act & Assert
        await PostAsync("/projects", new { name = "" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_whitespace_only_name_is_invalid()
    {
        // Act & Assert
        await PostAsync("/projects", new { name = "   " }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_name_1_char_after_trim_is_valid()
    {
        // Act
        var result = await PostAsync("/projects", new { name = "  x  " });

        // Assert
        Assert.Equal("x", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_name_100_chars_after_trim_is_valid()
    {
        // Arrange
        var name = " " + new string('a', 100) + " ";

        // Act
        var result = await PostAsync("/projects", new { name });

        // Assert
        Assert.Equal(new string('a', 100), result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_name_101_chars_after_trim_is_invalid()
    {
        // Arrange
        var name = " " + new string('a', 101) + " ";

        // Act & Assert
        await PostAsync("/projects", new { name }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_project_stores_name_trimmed()
    {
        // Act
        var result = await PostAsync("/projects", new { name = "  Garden  " });

        // Assert
        Assert.Equal("Garden", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_project_duplicate_name_returns_409()
    {
        // Arrange
        await CreateProjectAsync("Garden");

        // Act & Assert
        await PostAsync("/projects", new { name = "Garden" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_project_duplicate_name_case_insensitive_returns_409()
    {
        // Arrange
        await CreateProjectAsync("Garden");

        // Act & Assert
        await PostAsync("/projects", new { name = "garden" }, HttpStatusCode.Conflict);
    }

    // PUT /items/{id}/project

    [Fact]
    public async Task Assign_item_to_project_returns_200_with_item()
    {
        // Arrange
        var itemId = await CreateItemAsync("Fix the fence");
        var projectId = await CreateProjectAsync("Garden");

        // Act
        var result = await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId },
            HttpStatusCode.OK
        );

        // Assert
        Assert.Equal(itemId, result.GetProperty("id").GetInt32());
        Assert.Equal("Fix the fence", result.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Assign_item_to_unknown_project_returns_400()
    {
        // Arrange
        var itemId = await CreateItemAsync("Task");

        // Act & Assert
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = 99999 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assign_unknown_item_to_project_returns_404()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");

        // Act & Assert
        await SendAsync(HttpMethod.Put, "/items/99999/project", new { projectId }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_item_from_project_returns_200()
    {
        // Arrange
        var itemId = await CreateItemAsync("Fix the fence");
        var projectId = await CreateProjectAsync("Garden");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        var result = await SendAsync(
            HttpMethod.Put,
            $"/items/{itemId}/project",
            new { projectId = (int?)null },
            HttpStatusCode.OK
        );

        // Assert
        Assert.Equal(itemId, result.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Removed_item_no_longer_appears_in_project()
    {
        // Arrange
        var itemId = await CreateItemAsync("Fix the fence");
        var projectId = await CreateProjectAsync("Garden");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = (int?)null }, HttpStatusCode.OK);

        // Assert
        var detail = await GetAsync($"/projects/{projectId}");
        Assert.Equal([], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Item_moved_to_another_project_leaves_first_project()
    {
        // Arrange
        var itemId = await CreateItemAsync("Task");
        var projectA = await CreateProjectAsync("Project A");
        var projectB = await CreateProjectAsync("Project B");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = projectA }, HttpStatusCode.OK);

        // Act
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId = projectB }, HttpStatusCode.OK);

        // Assert
        var detailA = await GetAsync($"/projects/{projectA}");
        Assert.Equal([], Titles(detailA.GetProperty("items")));
    }

    // GET /projects

    [Fact]
    public async Task List_projects_includes_all_stats_fields()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var item1 = await CreateItemAsync("Task one");
        var item2 = await CreateItemAsync("Task two");
        var item3 = await CreateItemAsync("Task three");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item3}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(item1);

        // Act
        var list = await GetAsync("/projects");

        // Assert
        var project = list.EnumerateArray().Single();
        Assert.Equal(projectId, project.GetProperty("id").GetInt32());
        Assert.Equal("Garden", project.GetProperty("name").GetString());
        Assert.Equal(3, project.GetProperty("total").GetInt32());
        Assert.Equal(1, project.GetProperty("done").GetInt32());
        Assert.Equal(33, project.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task Project_with_no_items_is_zero_percent_done()
    {
        // Arrange
        await CreateProjectAsync("Empty Project");

        // Act
        var list = await GetAsync("/projects");

        // Assert
        var project = list.EnumerateArray().Single();
        Assert.Equal(0, project.GetProperty("total").GetInt32());
        Assert.Equal(0, project.GetProperty("done").GetInt32());
        Assert.Equal(0, project.GetProperty("percentDone").GetInt32());
    }

    [Fact]
    public async Task List_projects_sorted_by_name_case_insensitive()
    {
        // Arrange — created in a different order to verify that sorting, not insertion order, determines the result
        await CreateProjectAsync("Cherry");
        await CreateProjectAsync("apple");
        await CreateProjectAsync("Banana");

        // Act
        var list = await GetAsync("/projects");

        // Assert
        var names = list.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(["apple", "Banana", "Cherry"], names);
    }

    [Fact]
    public async Task Percent_done_is_floored_not_rounded()
    {
        // Arrange — 2 done out of 3: 66.66...% truncated to 66, not rounded to 67
        var projectId = await CreateProjectAsync("Garden");
        var item1 = await CreateItemAsync("Task one");
        var item2 = await CreateItemAsync("Task two");
        var item3 = await CreateItemAsync("Task three");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item3}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(item1);
        await MarkDoneAsync(item2);

        // Act
        var list = await GetAsync("/projects");

        // Assert
        Assert.Equal(66, list.EnumerateArray().Single().GetProperty("percentDone").GetInt32());
    }

    // GET /projects/{id}

    [Fact]
    public async Task Get_project_unknown_returns_404()
    {
        // Act & Assert
        await GetAsync("/projects/99999", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_project_lists_open_items_before_done_items()
    {
        // Arrange — done item created first so a naive id-desc sort would put it last anyway;
        // open item created second to confirm the group ordering, not just id ordering
        var projectId = await CreateProjectAsync("Garden");
        var doneId = await CreateItemAsync("Done task");
        var openId = await CreateItemAsync("Open task");
        await SendAsync(HttpMethod.Put, $"/items/{doneId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{openId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(doneId);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["Open task", "Done task"], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_open_items_are_newest_first()
    {
        // Arrange — older item added first (lower id); newest-first requires reversing creation order
        var projectId = await CreateProjectAsync("Garden");
        var olderId = await CreateItemAsync("Older task");
        var newerId = await CreateItemAsync("Newer task");
        await SendAsync(HttpMethod.Put, $"/items/{olderId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{newerId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["Newer task", "Older task"], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_done_items_are_newest_first()
    {
        // Arrange — older done item added first; newest-first within the done group requires reversing
        var projectId = await CreateProjectAsync("Garden");
        var olderDoneId = await CreateItemAsync("Older done");
        var newerDoneId = await CreateItemAsync("Newer done");
        await SendAsync(HttpMethod.Put, $"/items/{olderDoneId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{newerDoneId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(olderDoneId);
        await MarkDoneAsync(newerDoneId);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["Newer done", "Older done"], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_excludes_items_from_other_projects()
    {
        // Arrange
        var projectA = await CreateProjectAsync("Project A");
        var projectB = await CreateProjectAsync("Project B");
        var itemInA = await CreateItemAsync("Item A");
        var itemInB = await CreateItemAsync("Item B");
        await SendAsync(HttpMethod.Put, $"/items/{itemInA}/project", new { projectId = projectA }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{itemInB}/project", new { projectId = projectB }, HttpStatusCode.OK);

        // Act
        var detailA = await GetAsync($"/projects/{projectA}");

        // Assert
        Assert.Equal(["Item A"], Titles(detailA.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_excludes_unassigned_items()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var assignedId = await CreateItemAsync("Assigned task");
        await CreateItemAsync("Unassigned task");
        await SendAsync(HttpMethod.Put, $"/items/{assignedId}/project", new { projectId }, HttpStatusCode.OK);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(["Assigned task"], Titles(detail.GetProperty("items")));
    }

    [Fact]
    public async Task Get_project_items_have_same_shape_as_get_items()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var itemId = await CreateItemAsync("Fix the fence");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(itemId);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        var item = detail.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(itemId, item.GetProperty("id").GetInt32());
        Assert.Equal("Fix the fence", item.GetProperty("title").GetString());
        Assert.True(item.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Get_project_includes_stats()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var item1 = await CreateItemAsync("Task one");
        var item2 = await CreateItemAsync("Task two");
        await SendAsync(HttpMethod.Put, $"/items/{item1}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Put, $"/items/{item2}/project", new { projectId }, HttpStatusCode.OK);
        await MarkDoneAsync(item1);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal(2, detail.GetProperty("total").GetInt32());
        Assert.Equal(1, detail.GetProperty("done").GetInt32());
        Assert.Equal(50, detail.GetProperty("percentDone").GetInt32());
    }

    // DELETE /projects/{id}

    [Fact]
    public async Task Delete_empty_project_returns_204()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");

        // Act & Assert
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_project_with_items_returns_409()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var itemId = await CreateItemAsync("Task");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);

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
    public async Task Delete_refused_project_still_exists()
    {
        // Arrange
        var projectId = await CreateProjectAsync("Garden");
        var itemId = await CreateItemAsync("Task");
        await SendAsync(HttpMethod.Put, $"/items/{itemId}/project", new { projectId }, HttpStatusCode.OK);
        await SendAsync(HttpMethod.Delete, $"/projects/{projectId}", null, HttpStatusCode.Conflict);

        // Act
        var detail = await GetAsync($"/projects/{projectId}");

        // Assert
        Assert.Equal("Garden", detail.GetProperty("name").GetString());
    }
}
