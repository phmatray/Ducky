using Ducky.Builder;
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
        services.AddDuckyStore(builder => builder
            .EnableDiagnostics()
            .AddNoOpMiddleware());

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
        services.AddDuckyStore(builder => builder
            .EnableDiagnostics()
            .AddNoOpMiddleware()
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
        Assert.Equal(1, report.TotalMiddlewares);
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
        services.AddDuckyStore(builder => builder
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
        services.AddDuckyStore(builder => builder
            .EnableDiagnostics()
            .AddMiddleware<ErrorMiddleware>()
            .ConfigureStore(options => options.AssemblyNames = ["Ducky.Tests"]));

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        MiddlewareDiagnostics diagnostics = provider.GetRequiredService<MiddlewareDiagnostics>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            dispatcher.Dispatch(new TestAction());
            await Task.Delay(50, TestContext.Current.CancellationToken);
        });

        MiddlewareDiagnosticReport report = diagnostics.GetReport();
        MiddlewareReport? errorReport = report.Middlewares.FirstOrDefault(m => m.Info.Type == typeof(ErrorMiddleware));

        Assert.NotNull(errorReport);
        Assert.Equal(1, errorReport.Metrics.Errors);
        Assert.NotNull(errorReport.Metrics.LastError);
        Assert.Equal("Test error", errorReport.Metrics.LastError.Exception.Message);
    }

    [Fact]
    public void GetReport_ReturnsCompleteInformation()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDuckyStore(builder => builder
            .EnableDiagnostics()
            .AddNoOpMiddleware()
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
        Assert.Equal(2, report.TotalMiddlewares);
        Assert.Equal(2, report.Middlewares.Count);
        Assert.True(report.GeneratedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(0, report.TotalErrors);

        // Check middleware ordering
        Assert.Equal(0, report.Middlewares[0].Info.Order);
        Assert.Equal(1, report.Middlewares[1].Info.Order);
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
            throw new InvalidOperationException("Test error");
        }
    }
}
