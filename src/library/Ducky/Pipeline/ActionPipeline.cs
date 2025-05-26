using R3;

namespace Ducky.Pipeline;

/// <summary>
/// A reactive, ordered middleware pipeline that processes actions before and after reducers.
/// </summary>
public sealed class ActionPipeline : IDisposable
{
    private readonly Subject<ActionContext> _incoming = new();
    private readonly List<Func<Observable<ActionContext>, Observable<ActionContext>>> _beforeMiddlewares = [];
    private readonly List<Func<Observable<ActionContext>, Observable<ActionContext>>> _afterMiddlewares = [];
    private Observable<ActionContext>? _builtBefore;
    private Observable<ActionContext>? _builtAfter;
    private readonly IDisposable _dispatcherSub;

    /// <summary>
    /// Creates a new pipeline that listens to the dispatcher’s ActionStream.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to listen to.</param>
    public ActionPipeline(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcherSub = dispatcher.ActionStream
            .Select(a => new ActionContext(a))
            .Subscribe(ctx => _incoming.OnNext(ctx));
    }

    /// <summary>
    /// Registers a middleware instance.
    /// </summary>
    /// <param name="middleware">The middleware instance to register.</param>
    /// <returns>The current <see cref="ActionPipeline"/> instance for chaining.</returns>
    public ActionPipeline Use(IActionMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _beforeMiddlewares.Add(middleware.InvokeBeforeReduce);
        _afterMiddlewares.Add(middleware.InvokeAfterReduce);
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
        _beforeMiddlewares.Add(src => src.SelectMany(ctx => func(ctx.Action)));
        _builtBefore = null;
        return this;
    }

    /// <summary>
    /// Registers a simple after-reduce middleware function.
    /// </summary>
    public ActionPipeline UseAfter(Func<object, Observable<ActionContext>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _afterMiddlewares.Add(src => src.SelectMany(ctx => func(ctx.Action)));
        _builtAfter = null;
        return this;
    }

    /// <summary>
    /// The composed stream of contexts before all reducers, after all before-middlewares.
    /// </summary>
    public Observable<ActionContext> BeforeReducePipeline
    {
        get
        {
            if (_builtBefore is not null)
            {
                return _builtBefore;
            }

            Observable<ActionContext> seq = _beforeMiddlewares.Aggregate(
                _incoming.AsObservable(),
                (current, mw) => mw(current));

            _builtBefore = seq.Publish().RefCount();
            return _builtBefore;
        }
    }

    /// <summary>
    /// The composed stream of contexts after all reducers, after all after-middlewares (in reverse registration order).
    /// </summary>
    public Observable<ActionContext> AfterReducePipeline
    {
        get
        {
            if (_builtAfter is not null)
            {
                return _builtAfter;
            }

            Observable<ActionContext> seq = _afterMiddlewares
                .AsReadOnly()
                .Reverse()
                .Aggregate(
                    _incoming.AsObservable(),
                    (current, mw) => mw(current));

            _builtAfter = seq.Publish().RefCount();
            return _builtAfter;
        }
    }

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
        _dispatcherSub.Dispose();
        _incoming.OnCompleted();
        _incoming.Dispose();
    }
}
