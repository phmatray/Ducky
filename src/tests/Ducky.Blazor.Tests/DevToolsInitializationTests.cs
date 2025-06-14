using FakeItEasy;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests;

public class DevToolsInitializationTests
{
    [Fact]
    public void ReduxDevToolsModule_CanBeCreated_WithoutStoreAndDispatcher()
    {
        // Arrange
        IJSRuntime mockJsRuntime = A.Fake<IJSRuntime>();
        DevToolsStateManager stateManager = new();
        DevToolsOptions options = new();

        // Act
        ReduxDevToolsModule devTools = new(mockJsRuntime, stateManager, options);

        // Assert
        Assert.NotNull(devTools);
        Assert.False(devTools.IsEnabled);
    }

    [Fact]
    public async Task ReduxDevToolsModule_InitAsync_ReturnsWhenDisabledAsync()
    {
        // Arrange
        IJSRuntime mockJsRuntime = A.Fake<IJSRuntime>();
        DevToolsStateManager stateManager = new();
        DevToolsOptions options = new() { Enabled = false };
        ReduxDevToolsModule devTools = new(mockJsRuntime, stateManager, options);

        // Act
        await devTools.InitAsync().ConfigureAwait(true);

        // Assert
        Assert.False(devTools.IsEnabled);
        A.CallTo(() => mockJsRuntime.InvokeAsync<IJSObjectReference>(
            A<string>.Ignored,
            A<object[]>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public void DevToolsMiddleware_CanInitialize_WithDevTools()
    {
        // Arrange
        IJSRuntime mockJsRuntime = A.Fake<IJSRuntime>();
        DevToolsStateManager stateManager = new();
        ReduxDevToolsModule devTools = new(mockJsRuntime, stateManager);
        DevToolsMiddleware middleware = new(devTools);

        IStore mockStore = A.Fake<IStore>();
        IDispatcher mockDispatcher = A.Fake<IDispatcher>();

        // Act & Assert (should not throw)
        Task task = middleware.InitializeAsync(mockDispatcher, mockStore);
        Assert.NotNull(task);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void DuckyBlazor_Registration_Works()
    {
        // Arrange
        ServiceCollection services = [];

        // Add required dependencies
        services.AddSingleton(A.Fake<IJSRuntime>());

        // Save original environment variables
        string? originalAspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        string? originalDotNetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        try
        {
            // Set environment to avoid development defaults
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");

            // Act
            services.AddDuckyBlazor(ducky => ducky
                .EnableDevTools(options =>
                {
                    options.StoreName = "TestStore";
                    options.Enabled = false; // Disable for testing
                }));

            // Assert
            ServiceProvider provider = services.BuildServiceProvider();
            ReduxDevToolsModule? devToolsModule = provider.GetService<ReduxDevToolsModule>();
            DevToolsOptions? devToolsOptions = provider.GetService<DevToolsOptions>();

            Assert.NotNull(devToolsModule);
            Assert.NotNull(devToolsOptions);
            Assert.Equal("TestStore", devToolsOptions.StoreName);
            Assert.False(devToolsOptions.Enabled);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetCoreEnv);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotNetEnv);
        }
    }
}
