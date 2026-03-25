// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : MiddlewareBase, IDisposable
{
    private readonly IEnumerable<IAsyncEffect> _effects;
    private readonly IStoreEventPublisher _eventPublisher;
    private CancellationTokenSource _cts = new();
    private IStore? _store;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEffectMiddleware"/> with the specified dependencies.
    /// </summary>
    /// <param name="effects">An enumerable collection of asynchronous effects that implement <see cref="IAsyncEffect"/>.</param>
    /// <param name="eventPublisher">The store event publisher for error events.</param>
    public AsyncEffectMiddleware(
        IEnumerable<IAsyncEffect> effects,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _effects = effects;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;

        // Inject the dispatcher into each effect
        foreach (IAsyncEffect effect in _effects)
        {
            effect.SetDispatcher(dispatcher);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns true. This middleware should not block actions from reaching
    /// other middleware and reducers, even if no async effect handles the action.
    /// </remarks>
    public override bool MayDispatchAction(object action)
    {
        return true;
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        TriggerEffects(action);
    }

    /// <summary>
    /// Cancels all pending effects and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }

    private void TriggerEffects(object action)
    {
        IAsyncEffect[] effectsToExecute = _effects
            .Where(effect => effect.CanHandle(action))
            .ToArray();

        if (effectsToExecute.Length == 0)
        {
            return;
        }

        CancellationToken token = _cts.Token;

        // Track each effect alongside its task for reliable error attribution
        List<(IAsyncEffect Effect, Task Task)> effectTasks = new(effectsToExecute.Length);

        // Execute all effects. Some will execute synchronously and complete immediately,
        // so we need to catch their exceptions in the loop so they don't prevent
        // other effects from executing.
        foreach (IAsyncEffect effect in effectsToExecute)
        {
            try
            {
                Task task = effect.HandleAsync(action, _store!, token);
                effectTasks.Add((effect, task));
            }
            catch (Exception exception)
            {
                // Synchronous failure — publish immediately with correct attribution
                _eventPublisher.Publish(new EffectErrorEventArgs(exception, effect.GetType(), action));
            }
        }

        if (effectTasks.Count == 0)
        {
            return;
        }

        // Fire and forget - handle all async effects concurrently
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.WhenAll(effectTasks.Select(et => et.Task)).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow — we inspect individual tasks below for accurate attribution
                }

                // Publish errors with correct effect attribution
                foreach ((IAsyncEffect effect, Task task) in effectTasks)
                {
                    if (task is { IsFaulted: true, Exception: not null })
                    {
                        foreach (Exception innerEx in task.Exception.Flatten().InnerExceptions)
                        {
                            _eventPublisher.Publish(new EffectErrorEventArgs(innerEx, effect.GetType(), action));
                        }
                    }
                }
            },
            token);
    }
}
