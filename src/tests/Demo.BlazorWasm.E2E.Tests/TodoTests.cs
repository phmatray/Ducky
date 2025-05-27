namespace Demo.BlazorWasm.E2E.Tests;

[TestFixture]
public class TodoTests : TestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await TestSetup();
        await NavigateAndWaitForBlazor("/todo");
    }

    [Test]
    public async Task Todo_ShouldAddNewItem()
    {
        // Arrange
        // MudTextField renders the label as text above the input
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");
        const string todoText = "Test todo item";

        // Act
        await input.FillAsync(todoText);
        await addButton.ClickAsync();

        // Assert
        ILocator todoItem = Page.Locator($"text={todoText}");
        await Expect(todoItem).ToBeVisibleAsync();

        // Check input is cleared
        await Expect(input).ToHaveValueAsync(string.Empty);
    }

    [Test]
    public async Task Todo_ShouldToggleCompletion()
    {
        // Arrange - Add a todo first
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");
        await input.FillAsync("Todo to complete");
        await addButton.ClickAsync();

        // Act - Click the unchecked checkbox icon
        // The checkbox is the first button in the active todos section
        ILocator uncheckedIcon = Page.Locator(".mud-list-item .mud-icon-button").First;
        await uncheckedIcon.ClickAsync();

        // Assert - Check item moved to completed section
        ILocator completedSection = Page.Locator("h5:has-text('Completed Todos')").Locator("..");
        ILocator completedItem = completedSection.Locator("text=Todo to complete");
        await Expect(completedItem).ToBeVisibleAsync();

        // Check strikethrough style - verify item is in completed section with strikethrough
        ILocator strikethroughText = completedSection.Locator("text=Todo to complete");
        await Expect(strikethroughText).ToHaveAttributeAsync("style", new Regex("text-decoration.*line-through"));
    }

    [Test]
    public async Task Todo_ShouldDeleteItem()
    {
        // Arrange - Add a todo first
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");
        const string todoText = "Todo to delete";
        await input.FillAsync(todoText);
        await addButton.ClickAsync();

        // Verify it exists
        ILocator todoItem = Page.Locator($"text={todoText}");
        await Expect(todoItem).ToBeVisibleAsync();

        // Act - Delete the item
        ILocator listItem = Page.Locator(".mud-list-item").Filter(new() { HasText = todoText });
        ILocator deleteButton = listItem.Locator("button:has-text('Delete')");
        await deleteButton.ClickAsync();

        // Assert - Item should be removed
        await Expect(todoItem).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Todo_ShouldShowActiveCount()
    {
        // Add multiple todos
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");

        await input.FillAsync("First todo");
        await addButton.ClickAsync();

        await input.FillAsync("Second todo");
        await addButton.ClickAsync();

        await input.FillAsync("Third todo");
        await addButton.ClickAsync();

        // Check active count badge
        ILocator activeBadge = Page.Locator(".mud-badge")
            .Filter(new() { Has = Page.Locator("h5:has-text('Active Todos')") });
        ILocator badgeContent = activeBadge.Locator(".mud-badge-content");
        await Expect(badgeContent).ToHaveTextAsync("3");

        // Complete one item
        ILocator firstUncheckedIcon = Page.Locator(".mud-list-item .mud-icon-button").First;
        await firstUncheckedIcon.ClickAsync();

        // Check count updated
        await Expect(badgeContent).ToHaveTextAsync("2");
    }

    [Test]
    public async Task Todo_ShouldShowCompletedCount()
    {
        // Add todos and complete some
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");

        await input.FillAsync("Active todo");
        await addButton.ClickAsync();

        await input.FillAsync("To be completed");
        await addButton.ClickAsync();

        // Complete the second todo
        ILocator uncheckedIcons = Page.Locator(".mud-list-item .mud-icon-button");
        await uncheckedIcons.Nth(1).ClickAsync();

        // Check completed count badge
        ILocator completedBadge = Page.Locator(".mud-badge")
            .Filter(new() { Has = Page.Locator("h5:has-text('Completed Todos')") });
        ILocator badgeContent = completedBadge.Locator(".mud-badge-content");
        await Expect(badgeContent).ToHaveTextAsync("1");
    }

    [Test]
    public async Task Todo_ShouldShowGoalAchievements()
    {
        // Add 5 todos to trigger the goal
        ILocator input = Page.Locator("input[type='text']").First;
        ILocator addButton = Page.Locator("button:has-text('Add Todo')");

        for (int i = 1; i <= 5; i++)
        {
            await input.FillAsync($"Todo {i}");
            await addButton.ClickAsync();
        }

        // Check goal is achieved
        ILocator activeGoal = Page.Locator("text=The number of active todos is 5 or more");
        await Expect(activeGoal).ToBeVisibleAsync();

        // Complete all todos
        for (int i = 0; i < 5; i++)
        {
            // Always click the first checkbox as items move when completed
            ILocator firstCheckbox = Page.Locator(".mud-list-item .mud-icon-button").First;
            await firstCheckbox.ClickAsync();
            await Task.Delay(100); // Small delay to ensure UI updates
        }

        // Check completed goal is achieved
        ILocator completedGoal = Page.Locator("text=The number of completed todos is 5 or more");
        await Expect(completedGoal).ToBeVisibleAsync();

        // Check no active todos goal
        ILocator noActiveGoal = Page.Locator("text=There are no active todos");
        await Expect(noActiveGoal).ToBeVisibleAsync();
    }
}