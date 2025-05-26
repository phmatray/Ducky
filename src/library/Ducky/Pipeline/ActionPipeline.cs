using R3;

namespace Ducky.Pipeline;

/// <summary>
/// A reactive, ordered middleware pipeline that processes actions before and after reducers.
/// Guarantees proper nesting order: Before Outer → Before Inner → Reduce → After Inner → After Outer.
/// </summary>
public sealed class ActionPipeline : IDisposable
{
    private readonly Subject<ActionContext> _incoming = new();
    private readonly Subject<ActionContext> _reduceNotification = new();
    private readonly Subject<ActionContext> _afterReduceSource = new();
    private readonly List<IActionMiddleware> _middlewares = [];
    private Observable<ActionContext>? _builtBefore;
    private Observable<ActionContext>? _builtAfter;
    private readonly IDisposable _dispatcherSub;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>
    /// Creates a new pipeline that listens to the dispatcher's ActionStream.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to listen to.</param>
    public ActionPipeline(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcherSub = dispatcher.ActionStream
            .Select(a => new ActionContext(a))
            .Subscribe(ctx => _incoming.OnNext(ctx));

        _disposables.Add(_dispatcherSub);
    }

    /// <summary>
    /// Registers a middleware instance.
    /// </summary>
    /// <param name="middleware">The middleware instance to register.</param>
    /// <returns>The current <see cref="ActionPipeline"/> instance for chaining.</returns>
    public ActionPipeline Use(IActionMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(middleware);
        _builtBefore = null;
        _builtAfter = null;
        return this;
    }

    /// <summary>
    /// Registers a simple before-reduce middleware function.
    /// </summary>
    public ActionPipeline UseBefore(Func<object, Observable<ActionContext>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Use(new SimpleBeforeMiddleware(func));
        return this;
    }

    /// <summary>
    /// Registers a simple after-reduce middleware function.
    /// </summary>
    public ActionPipeline UseAfter(Func<object, Observable<ActionContext>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Use(new SimpleAfterMiddleware(func));
        return this;
    }

    /// <summary>
    /// The composed stream of contexts before all reducers, after all before-middlewares.
    /// </summary>
    public Observable<ActionContext> BeforeReducePipeline
    {
        get
        {
            BuildPipelines();
            return _builtBefore!;
        }
    }

    /// <summary>
    /// The composed stream of contexts after all reducers, after all after-middlewares (in reverse registration order).
    /// </summary>
    public Observable<ActionContext> AfterReducePipeline
    {
        get
        {
            BuildPipelines();
            return _builtAfter!;
        }
    }

    private void BuildPipelines()
    {
        if (_builtBefore is not null && _builtAfter is not null)
        {
            return;
        }

        // Build the before pipeline: apply middlewares in order
        Observable<ActionContext> beforePipeline = _incoming.AsObservable();

        foreach (IActionMiddleware middleware in _middlewares)
        {
            beforePipeline = middleware.InvokeBeforeReduce(beforePipeline);
        }

        // Build the after pipeline: apply middlewares in reverse order
        Observable<ActionContext> afterPipeline = _afterReduceSource.AsObservable();

        // Apply middlewares in reverse order to maintain proper nesting
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            afterPipeline = _middlewares[i].InvokeAfterReduce(afterPipeline);
        }

        // Create the connection between before and after through reduce notification
        // This ensures that when before pipeline completes, it triggers the after pipeline
        Observable<ActionContext> connectedBefore = beforePipeline
            .Do(ctx =>
            {
                if (ctx.IsAborted)
                {
                    return;
                }

                _reduceNotification.OnNext(ctx);
                _afterReduceSource.OnNext(ctx);
            })
            .Publish()
            .RefCount();

        _builtBefore = connectedBefore;
        _builtAfter = afterPipeline.Publish().RefCount();

        // Ensure the before pipeline is active to trigger the connection
        _disposables.Add(connectedBefore.Subscribe(_ => { }));
    }

    /// <summary>
    /// Notifies when the reduce phase should occur (between before and after pipelines).
    /// </summary>
    public Observable<ActionContext> ReduceNotification => _reduceNotification.AsObservable();

    /// <summary>
    /// Subscribes to the specified before-reduce pipeline observer.
    /// </summary>
    public IDisposable SubscribeBefore(Observer<ActionContext> observer)
    {
        return BeforeReducePipeline.Subscribe(observer);
    }

    /// <summary>
    /// Subscribes to the specified after-reduce pipeline observer.
    /// </summary>
    public IDisposable SubscribeAfter(Observer<ActionContext> observer)
    {
        return AfterReducePipeline.Subscribe(observer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposables.Dispose();
        _incoming.OnCompleted();
        _incoming.Dispose();
        _reduceNotification.OnCompleted();
        _reduceNotification.Dispose();
        _afterReduceSource.OnCompleted();
        _afterReduceSource.Dispose();
    }

    // Helper middleware for simple functions
    private sealed class SimpleBeforeMiddleware : IActionMiddleware
    {
        private readonly Func<object, Observable<ActionContext>> _func;

        public SimpleBeforeMiddleware(Func<object, Observable<ActionContext>> func)
        {
            _func = func;
        }

        public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
        {
            return src.SelectMany(ctx => _func(ctx.Action));
        }

        public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
        {
            return src; // No-op for after
        }
    }

    private sealed class SimpleAfterMiddleware : IActionMiddleware
    {
        private readonly Func<object, Observable<ActionContext>> _func;

        public SimpleAfterMiddleware(Func<object, Observable<ActionContext>> func)
        {
            _func = func;
        }

        public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
        {
            return src; // No-op for before
        }

        public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
        {
            return src.SelectMany(ctx => _func(ctx.Action));
        }
    }
}
