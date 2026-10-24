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

        // On the drainer, before any pipeline subscribes: ReactiveMiddleware writes the property only when a snapshot
        // changed, so in a store where nothing changed state yet it still holds null, and it must never replay null.
        _bridge.State.Value = context.State;

        // Disposed by the store's lifetime cancellation (§6.11 step 2); an item added after that is disposed at once. Each
        // subscription is isolated: R3's CompositeDisposable stops at the first throwing teardown (user code), which would
        // leave every later pipeline running after the store is disposed (INV-27).
        var subscriptions = new CompositeDisposable();
        cancellationToken.Register(subscriptions.Dispose);
        foreach (var effect in _effects)
        {
            // Each start is isolated too: a Handle (or its Subscribe) that throws must not skip the later pipelines, nor the
            // StoreInitialized push below that the earlier ones wait for.
            try
            {
                subscriptions.Add(new IsolatedSubscription(effect.Handle(_bridge.Actions, _bridge.State).Subscribe(context.Dispatch), effect.GetType(), _logger));
                Log.EffectSubscribed(_logger, effect.GetType());
            }
#pragma warning disable CA1031 // justification: Handle is user code; its throw is logged and never stops the rest (INV-27)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // ponytail: logged only; M11-02 publishes it as DispatchSystem(EffectFailed(type, "reactive", ex), isFailure: true).
                Log.EffectStartThrew(_logger, ex, effect.GetType());
            }
        }

        // ReactiveMiddleware pushed StoreInitialized before any pipeline existed: push it once more, for them.
        _bridge.Actions.OnNext(context.Trigger.Action);
        return Task.CompletedTask;
    }

    // Like the core's store-owned instances (§6.11 5b): reverse construction order, each disposal isolated, so one
    // throwing effect never leaves the others undisposed (INV-29).
    public async ValueTask DisposeAsync()
    {
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
