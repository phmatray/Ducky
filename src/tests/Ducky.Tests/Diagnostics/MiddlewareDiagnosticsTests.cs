using Ducky.Diagnostics;
using Ducky.Middlewares.NoOp;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

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
        diagnostics.ShouldNotBeNull();
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
        report.ShouldNotBeNull();
        report.TotalMiddlewares.ShouldBe(3); // Default 2 + NoOp
        report.TotalExecutions.ShouldBeGreaterThan(0);

        MiddlewareReport? noOpReport = report.Middlewares.FirstOrDefault(m => m.Info.Type == typeof(NoOpMiddleware));
        noOpReport.ShouldNotBeNull();
        noOpReport.Metrics.TotalExecutions.ShouldBeGreaterThan(0);
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

        delayReport.ShouldNotBeNull();
        delayReport.Metrics.TotalExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
        delayReport.Metrics.AverageBeforeReduceTime.ShouldBeGreaterThan(TimeSpan.Zero);
        delayReport.Metrics.AverageAfterReduceTime.ShouldBeGreaterThan(TimeSpan.Zero);
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

        errorReport.ShouldNotBeNull();
        // The error might be recorded multiple times (BeforeReduce/AfterReduce)
        errorReport.Metrics.Errors.ShouldBeGreaterThanOrEqualTo(1);
        errorReport.Metrics.LastError.ShouldNotBeNull();
        errorReport.Metrics.LastError.Exception.Message.ShouldBe("Test error");
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
        report.ShouldNotBeNull();
        report.TotalMiddlewares.ShouldBe(4); // Default 2 + NoOp + Delay
        report.Middlewares.Count.ShouldBe(4);
        report.GeneratedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        report.TotalErrors.ShouldBe(0);

        // Check that middlewares are ordered
        for (int i = 0; i < report.Middlewares.Count - 1; i++)
        {
            report.Middlewares[i].Info.Order.ShouldBeLessThanOrEqualTo(report.Middlewares[i + 1].Info.Order);
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
