using Ducky.Diagnostics;
using Ducky.Middlewares.NoOp;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Diagnostics;

public class MiddlewareDiagnosticsTests
{
    [Fact]
    public void EnableDiagnostics_RegistersDiagnosticsService()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<NoOpMiddleware>());

        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        MiddlewareDiagnostics? diagnostics = provider.GetService<MiddlewareDiagnostics>();
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public async Task DiagnosticMiddleware_RecordsMiddlewareExecution()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<NoOpMiddleware>()
            .ConfigureStore(options => options.AssemblyNames = ["Ducky.Tests"]));

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        MiddlewareDiagnostics diagnostics = provider.GetRequiredService<MiddlewareDiagnostics>();

        // Act
        dispatcher.Dispatch(new TestAction());
        await Task.Delay(50, TestContext.Current.CancellationToken); // Allow time for action processing

        // Assert
        MiddlewareDiagnosticReport report = diagnostics.GetReport();
        Assert.NotNull(report);
        Assert.Equal(3, report.TotalMiddlewares); // Default 2 + NoOp
        Assert.True(report.TotalExecutions > 0);

        MiddlewareReport? noOpReport = report.Middlewares.FirstOrDefault(m => m.Info.Type == typeof(NoOpMiddleware));
        Assert.NotNull(noOpReport);
        Assert.True(noOpReport.Metrics.TotalExecutions > 0);
    }

    [Fact]
    public async Task DiagnosticMiddleware_RecordsExecutionTimes()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<DelayMiddleware>()
            .ConfigureStore(options => options.AssemblyNames = ["Ducky.Tests"]));

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        MiddlewareDiagnostics diagnostics = provider.GetRequiredService<MiddlewareDiagnostics>();

        // Act
        dispatcher.Dispatch(new TestAction());
        await Task.Delay(150, TestContext.Current.CancellationToken); // Allow time for action processing

        // Assert
        MiddlewareDiagnosticReport report = diagnostics.GetReport();
        MiddlewareReport? delayReport = report.Middlewares.FirstOrDefault(m => m.Info.Type == typeof(DelayMiddleware));

        Assert.NotNull(delayReport);
        Assert.True(delayReport.Metrics.TotalExecutionTime > TimeSpan.Zero);
        Assert.True(delayReport.Metrics.AverageBeforeReduceTime > TimeSpan.Zero);
        Assert.True(delayReport.Metrics.AverageAfterReduceTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task DiagnosticMiddleware_RecordsErrors()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<ErrorMiddleware>()
            .ConfigureStore(options => options.AssemblyNames = ["Ducky.Tests"]));

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        MiddlewareDiagnostics diagnostics = provider.GetRequiredService<MiddlewareDiagnostics>();

        // Act
        try
        {
            dispatcher.Dispatch(new TestAction());
        }
        catch (InvalidOperationException)
        {
            // Expected - the error should be recorded even though it propagates
        }
        
        await Task.Delay(100, TestContext.Current.CancellationToken); // Allow time for error processing

        MiddlewareDiagnosticReport report = diagnostics.GetReport();
        MiddlewareReport? errorReport = report.Middlewares.FirstOrDefault(m => m.Info.Type == typeof(ErrorMiddleware));

        Assert.NotNull(errorReport);
        // The error might be recorded multiple times (BeforeReduce/AfterReduce)
        Assert.True(errorReport.Metrics.Errors >= 1);
        Assert.NotNull(errorReport.Metrics.LastError);
        Assert.Equal("Test error", errorReport.Metrics.LastError.Exception.Message);
    }

    [Fact]
    public void GetReport_ReturnsCompleteInformation()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<NoOpMiddleware>()
            .AddMiddleware<DelayMiddleware>()
            .ConfigureStore(options => options.AssemblyNames = ["Ducky.Tests"]));

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        MiddlewareDiagnostics diagnostics = provider.GetRequiredService<MiddlewareDiagnostics>();

        // Act
        dispatcher.Dispatch(new TestAction());
        Thread.Sleep(100); // Allow time for action processing

        MiddlewareDiagnosticReport report = diagnostics.GetReport();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(4, report.TotalMiddlewares); // Default 2 + NoOp + Delay
        Assert.Equal(4, report.Middlewares.Count);
        Assert.True(report.GeneratedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(0, report.TotalErrors);

        // Check that middlewares are ordered
        for (int i = 0; i < report.Middlewares.Count - 1; i++)
        {
            Assert.True(report.Middlewares[i].Info.Order <= report.Middlewares[i + 1].Info.Order);
        }
    }

    // Test helpers
    public record TestAction;

    public record TestState : IState;

    public record TestSliceReducers : SliceReducers<TestState>
    {
        public override TestState GetInitialState() => new();
    }

    private class DelayMiddleware : MiddlewareBase
    {
        public override void BeforeReduce(object action)
        {
            Thread.Sleep(10);
        }

        public override void AfterReduce(object action)
        {
            Thread.Sleep(10);
        }
    }

    private class ErrorMiddleware : MiddlewareBase
    {
        public override void BeforeReduce(object action)
        {
            // Only throw for TestAction, not for system actions like StoreInitialized
            if (action is not TestAction)
            {
                return;
            }
            
            throw new InvalidOperationException("Test error");
        }
    }
}
