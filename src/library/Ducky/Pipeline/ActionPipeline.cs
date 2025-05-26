using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _services;

    /// <summary>
    /// Creates a new pipeline that listens to the dispatcher’s ActionStream.
    /// </summary>
    public ActionPipeline(IDispatcher dispatcher, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(services);

        _dispatcherSub = dispatcher.ActionStream
            .Select(a => new ActionContext(a))
            .Subscribe(ctx => _incoming.OnNext(ctx));

        _services = services;
    }

    /// <summary>
    /// Registers a middleware by type. Must be registered in DI as <see cref="IActionMiddleware"/>.
    /// </summary>
    public ActionPipeline Use<TMiddleware>()
        where TMiddleware : IActionMiddleware
    {
        _beforeMiddlewares.Add(src =>
        {
            TMiddleware mw = _services.GetRequiredService<TMiddleware>();
            return mw.InvokeBeforeReduce(src);
        });
        _afterMiddlewares.Add(src =>
        {
            TMiddleware mw = _services.GetRequiredService<TMiddleware>();
            return mw.InvokeAfterReduce(src);
        });
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
