using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Pipeline.Reactive;

/// <summary>
/// A reactive, ordered middleware pipeline that subscribes to an action stream,
/// wraps each action in <see cref="ActionContext"/>, and applies registered middleware.
/// </summary>
public sealed class ActionPipeline : IDisposable
{
    private readonly Subject<ActionContext> _incoming = new();
    private readonly List<Func<Observable<ActionContext>, Observable<ActionContext>>> _middleware = [];
    private Observable<ActionContext>? _built;

    private readonly IDisposable _dispatcherSub;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Create a new pipeline that listens to <paramref name="dispatcher"/>’s ActionStream.
    /// </summary>
    public ActionPipeline(IDispatcher dispatcher, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(services);

        // subscribe raw actions → wrap in ActionContext → push into our internal Subject
        _dispatcherSub = dispatcher.ActionStream
            .Select(a => new ActionContext(a))
            .Subscribe(ctx => _incoming.OnNext(ctx));

        _services = services;
    }

    /// <summary>
    /// Register a middleware by type.  Must be registered in DI as <see cref="IActionMiddleware"/>.
    /// </summary>
    public ActionPipeline Use<TMiddleware>() where TMiddleware : IActionMiddleware
    {
        _middleware.Add(src =>
        {
            // resolve the middleware instance from your IServiceProvider
            TMiddleware mw = _services.GetRequiredService<TMiddleware>();
            return mw.Invoke(src);
        });

        _built = null; // invalidate cache
        return this;
    }

    /// <summary>
    /// The fully composed stream of contexts after all middleware.
    /// </summary>
    public Observable<ActionContext> Pipeline
    {
        get
        {
            if (_built is not null)
            {
                return _built;
            }

            // fold the incoming subject through each middleware
            Observable<ActionContext> seq = _incoming.AsObservable();
            foreach (Func<Observable<ActionContext>, Observable<ActionContext>> mw in _middleware)
            {
                seq = mw(seq);
            }

            // publish & refcount so multiple subscribers share one execution
            _built = seq.Publish().RefCount();
            return _built;
        }
    }

    /// <summary>
    /// Subscribe to the end of the pipeline.
    /// </summary>
    public IDisposable Subscribe(Observer<ActionContext> observer)
    {
        return Pipeline.Subscribe(observer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dispatcherSub.Dispose();
        _incoming.OnCompleted();
        _incoming.Dispose();
    }
}
