namespace Demo.BlazorWasm.E2E.Tests;

public class CounterTests : TestBase
{
    protected override async Task SetUp()
    {
        await base.SetUp();
        await NavigateAndWaitForBlazor("/counter");
    }

    [Fact]
    public async Task Counter_ShouldStartAtZero()
    {
        // The counter value is displayed in the h3 tag
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");
        await Expect(counterHeader).ToContainTextAsync("Counter 0");
    }

    [Fact]
    public async Task Counter_ShouldIncrementWhenClicked()
    {
        // Arrange
        ILocator incrementButton = Page.Locator("button:has-text('Increment')");
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");

        // Act - Increment by default amount (2)
        await incrementButton.ClickAsync();

        // Assert
        await Expect(counterHeader).ToContainTextAsync("Counter 2");

        // Click multiple times
        await incrementButton.ClickAsync();
        await incrementButton.ClickAsync();

        await Expect(counterHeader).ToContainTextAsync("Counter 6");
    }

    [Fact]
    public async Task Counter_ShouldDecrementWhenClicked()
    {
        // Arrange - First set to 10 using Reset button
        ILocator resetButton = Page.Locator("button:has-text('Set to 10')");
        ILocator decrementButton = Page.Locator("button:has-text('Decrement')");
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");

        // Set to 10
        await resetButton.ClickAsync();
        await Expect(counterHeader).ToContainTextAsync("Counter 10");

        // Act - Decrement by default amount (2)
        await decrementButton.ClickAsync();

        // Assert
        await Expect(counterHeader).ToContainTextAsync("Counter 8");

        // Decrement more
        await decrementButton.ClickAsync();
        await decrementButton.ClickAsync();

        await Expect(counterHeader).ToContainTextAsync("Counter 4");
    }

    [Fact]
    public async Task Counter_ShouldDisableDecrementAtZero()
    {
        // Arrange
        ILocator decrementButton = Page.Locator("button:has-text('Decrement')");
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");

        // Assert - Decrement should be disabled at 0
        await Expect(counterHeader).ToContainTextAsync("Counter 0");
        await Expect(decrementButton).ToBeDisabledAsync();

        // Increment and check it's enabled
        ILocator incrementButton = Page.Locator("button:has-text('Increment')");
        await incrementButton.ClickAsync();
        await Expect(decrementButton).Not.ToBeDisabledAsync();
    }

    [Fact]
    public async Task Counter_ShouldResetToTen()
    {
        // Arrange
        ILocator incrementButton = Page.Locator("button:has-text('Increment')");
        ILocator resetButton = Page.Locator("button:has-text('Set to 10')");
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");

        // Increment a few times
        await incrementButton.ClickAsync();
        await incrementButton.ClickAsync();
        await Expect(counterHeader).ToContainTextAsync("Counter 4");

        // Act - Reset
        await resetButton.ClickAsync();

        // Assert
        await Expect(counterHeader).ToContainTextAsync("Counter 10");
    }

    [Fact]
    public async Task Counter_ShouldIncrementByCustomAmount()
    {
        // Arrange
        ILocator incrementButton = Page.Locator("button:has-text('Increment')");
        ILocator amountInput = Page.Locator("input[type='number']");
        ILocator counterHeader = Page.Locator("h3:has-text('Counter')");

        // Act - Change amount to 5
        await amountInput.FillAsync("5");
        await incrementButton.ClickAsync();

        // Assert
        await Expect(counterHeader).ToContainTextAsync("Counter 5");

        // Change amount to 10 and increment again
        await amountInput.FillAsync("10");
        await incrementButton.ClickAsync();

        await Expect(counterHeader).ToContainTextAsync("Counter 15");
    }

    [Fact]
    public async Task Counter_ShouldShowProgressBar()
    {
        // Check progress bar exists
        ILocator progressBar = Page.Locator(".mud-progress-linear");
        await Expect(progressBar).ToBeVisibleAsync();

        // Progress should be indeterminate when counter > 15
        ILocator resetButton = Page.Locator("button:has-text('Set to 10')");
        ILocator incrementButton = Page.Locator("button:has-text('Increment')");
        ILocator amountInput = Page.Locator("input[type='number']");

        // Set to 10, then increment by 10 to reach 20
        await resetButton.ClickAsync();
        await amountInput.FillAsync("10");
        await incrementButton.ClickAsync();

        // Check the goals section shows the effect is triggered
        ILocator effectGoal = Page.Locator("text=The counter exceeds 15");
        await Expect(effectGoal).ToBeVisibleAsync();
    }
}