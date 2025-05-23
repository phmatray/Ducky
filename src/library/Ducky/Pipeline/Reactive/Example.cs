using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Pipeline.Reactive;

public static class Example
{
    public static void WireUp(IServiceCollection services)
    {
        services.AddSingleton<Dispatcher>(); // your IDispatcher
        services.AddSingleton<ActionPipeline>(); // pipeline (needs Dispatcher, IServiceProvider)
        services.AddSingleton<IActionMiddleware, CorrelationIdMiddleware>();
        services.AddSingleton<IActionMiddleware, LoggingMiddleware>();
        services.AddSingleton<IActionMiddleware, ValidationMiddleware>();

        // after building ServiceProvider:
        Dispatcher dispatcher = provider.GetRequiredService<Dispatcher>();
        ActionPipeline pipeline = new ActionPipeline(dispatcher, provider)
            .Use<CorrelationIdMiddleware>()
            .Use<LoggingMiddleware>()
            .Use<ValidationMiddleware>();

        // Subscribe your store (slices, reducers…) at the end:
        pipeline.Subscribe(ctx =>
        {
            if (ctx.IsAborted)
            {
                return;
            }

            // apply to slices or reducer
            Console.WriteLine($"[STORE] Reducing: {ctx.Action}");
        });

        // Dispatch actions:
        dispatcher.Dispatch(new DummyAction1("Hello"));
        dispatcher.Dispatch(new DummyAction2("Should be blocked"));
    }
}
