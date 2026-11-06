using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using R3;

namespace Ducky.Reactive;

// What AddReactiveEffect<T> records: the core never sees T (SPEC §14).
internal abstract class ReactiveEffectFactory
{
    public abstract ReactiveEffect Create(IServiceProvider services);
}

internal sealed class ReactiveEffectFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEffect> : ReactiveEffectFactory
    where TEffect : ReactiveEffect
{
    public override ReactiveEffect Create(IServiceProvider services) => ActivatorUtilities.CreateInstance<TEffect>(services);
}

// Starts every reactive pipeline on StoreInitialized (SPEC §14). A Merge run's token is the store-lifetime token, and the
// run ends as Handle returns, so pipelines are never counted for quiescence (INV-27).
internal sealed class ReactiveHost : Effect<StoreInitialized>, IAsyncDisposable
{
    private readonly ReactiveBridge _bridge;
    private readonly ReactiveEffect[] _effects;
    private readonly SafeLogger _logger;
    private bool _started;

    // Created from the store scope when the store materializes its effects; the store disposes this host, which disposes
    // the reactive effects it created.
    public ReactiveHost(ReactiveBridge bridge, IEnumerable<ReactiveEffectFactory> factories, IServiceProvider services)
    {
        _bridge = bridge;
        _logger = new((ILogger?)services.GetService<ILogger<ReactiveHost>>() ?? NullLogger.Instance);
        List<ReactiveEffect> built = [];
        try
        {
            foreach (var factory in factories)
            {
                built.Add(factory.Create(services));
            }
        }
        catch
        {
            // §6.6: the core never sees these instances, so a throwing constructor disposes the ones already built here,
            // in reverse order, each isolated. An asynchronous disposal is started, not awaited: DisposeOneAsync never
            // faults, so nothing is left unobserved.
            for (var i = built.Count - 1; i >= 0; i--)
            {
                _ = DisposeOneAsync(built[i]);
            }

            throw;
        }

        _effects = [.. built];
    }

    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        // Pipelines are subscribed once (§14): a user-dispatched StoreInitialized reaches them through ReactiveMiddleware
        // like any action, and starts nothing. Handle's synchronous prefix runs on the drainer, so a plain flag is enough.
        if (_started)
        {
            return Task.CompletedTask;
        }

        _started = true;

        // On the drainer, before any pipeline subscribes (§14): ReactiveMiddleware writes the bridge only when a snapshot
        // changed, so in a store where nothing changed state yet it would still hold null. ReactivePipeline's state starts
        // every subscription at the live snapshot, so no pipeline reads this value; the write keeps the bridge's subject
        // itself current, as §14 states it.
        // Stryker disable once Statement : equivalent, every pipeline subscription replaces the replayed value by the live snapshot (ReactivePipeline.State)
        _bridge.State.OnNext(context.State);

        // Disposed by the store's lifetime cancellation (§6.11 step 2); an item added after that is disposed at once. Each
        // subscription is isolated: R3's CompositeDisposable stops at the first throwing teardown (user code), which would
        // leave every later pipeline running after the store is disposed (INV-27).
        var subscriptions = new CompositeDisposable();
        cancellationToken.Register(subscriptions.Dispose);
        foreach (var effect in _effects)
        {
            // Each start is isolated (a throwing Handle is a reactive failure), so it never skips the later pipelines,
            // nor the StoreInitialized push below that the earlier ones wait for.
            new ReactivePipeline(effect, _bridge, context, subscriptions, _logger, cancellationToken).Subscribe();
        }

        // ReactiveMiddleware pushed StoreInitialized before any pipeline existed: push it once more, for them.
        _bridge.Actions.OnNext(context.Trigger.Action);
        return Task.CompletedTask;
    }

    // Like the core's store-owned instances (§6.11 5b): reverse construction order, each disposal isolated, so one
    // throwing effect never leaves the others undisposed (INV-29). The lifetime callback (§6.11 step 2) already disposed the
    // composite, so a pipeline Subscribe that takes the gate after this one finds its slot disposed, and one holding it
    // finishes its Handle first: Handle never runs on a disposed effect (§14, INV-27).
    public async ValueTask DisposeAsync()
    {
        lock (_bridge.Gate)
        {
        }

        for (var i = _effects.Length - 1; i >= 0; i--)
        {
            await DisposeOneAsync(_effects[i]).ConfigureAwait(ConfigureAwaitOptions.None);
        }
    }

    private async Task DisposeOneAsync(ReactiveEffect effect)
    {
        try
        {
            switch (effect)
            {
                case IAsyncDisposable disposable:
                    await disposable.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.None);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
#pragma warning disable CA1031 // justification: disposal is user code; its throw is logged and never stops the rest (§6.11)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.EffectDisposeThrew(_logger, ex, effect.GetType());
        }
    }
}

// A pipeline subscription whose teardown throw is logged (Error 3002) instead of escaping the composite that disposes it.
internal sealed class IsolatedSubscription(IDisposable inner, Type effectType, SafeLogger logger) : IDisposable
{
    public void Dispose()
    {
        try
        {
            inner.Dispose();
        }
#pragma warning disable CA1031 // justification: pipeline teardown is user code; its throw is logged and never stops the rest (INV-27)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.SubscriptionDisposeThrew(logger, ex, effectType);
        }
    }
}
