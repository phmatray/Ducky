using Ducky;
using Ducky.Pipeline;
using R3;

Console.WriteLine("Ducky Demo");

Dispatcher dispatcher = new();

ActionPipeline pipeline = new(dispatcher);
pipeline.Use(new OuterMiddleware());
pipeline.Use(new InnerMiddleware());

public sealed class OuterMiddleware : IActionMiddleware
{
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        Console.WriteLine("OuterMiddleware Before");
        return actions;
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        Console.WriteLine("OuterMiddleware After");
        return actions;
    }
}

public sealed class InnerMiddleware : IActionMiddleware
{
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        Console.WriteLine("InnerMiddleware Before");
        return actions;
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        Console.WriteLine("InnerMiddleware After");
        return actions;
    }
}
