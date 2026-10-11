using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Testing;

/// <summary>
/// Runs one action through an effect on a real <see cref="TestStore"/>, waits until the store is settled, and returns what
/// the effect dispatched. <see cref="DuckyBuilder.ThrowOnUnhandledAction"/> is turned off before <c>configure</c> runs
/// (which may turn it back on): the actions an effect dispatches usually have no reducer in the test, and they are results
/// to assert on, not failures.
/// </summary>
public static class EffectTest
{
#pragma warning disable RS0026, VSTHRD200 // justification: the spec's two Run overloads and their optional parameters (SPEC §15)
    /// <summary>Runs <paramref name="action"/> through <paramref name="effect"/>, which the store uses and never disposes.</summary>
    /// <typeparam name="TEffect">The effect type, an <see cref="EffectGroup"/> included.</typeparam>
    /// <typeparam name="TAction">The action type.</typeparam>
    /// <param name="effect">
    /// The effect; it replaces an <c>AddEffect&lt;TEffect&gt;()</c> that <paramref name="configure"/> registers. The replacement
    /// is keyed by <typeparamref name="TEffect"/>, so for a test double that derives from the real effect, pass the real
    /// effect type explicitly (<c>Run&lt;RealEffect, TAction&gt;(double, action)</c>), or both handlers run.
    /// </param>
    /// <param name="action">
    /// The action to dispatch. A <see cref="StoreInitialized"/> is not dispatched: the store's own one, processed at Ready
    /// (<see cref="Origin.System"/>), runs an <c>Effect&lt;StoreInitialized&gt;</c> once, as in production.
    /// </param>
    /// <param name="configure">Runs before the store is built: adds the slices the effect reads.</param>
    /// <param name="arrange">Runs on the built store, before the action: seeds state.</param>
    /// <returns>What the effect dispatched, and the failures.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/> (thrown synchronously, SPEC §8.1).</exception>
    /// <exception cref="DuckyAssertionException">The store was not settled, or not disposed, within its real-time bound (on the task); a disposal failure that follows another failure carries it as <see cref="Exception.InnerException"/>.</exception>
    public static Task<EffectRun> Run<TEffect, TAction>(
        TEffect effect, TAction action, Action<DuckyBuilder>? configure = null, Action<TestStore>? arrange = null)
        where TEffect : Effect
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(
            builder =>
            {
                builder.ThrowOnUnhandledAction = false;
                configure?.Invoke(builder);
                builder.AddEffect(effect);
            },
            action,
            arrange,
            services: null);
    }

    /// <summary>
    /// Runs <paramref name="action"/> through an effect the store builds and owns, so its constructor can inject the
    /// store-owned slices and SliceStores and the services <paramref name="services"/> registers.
    /// </summary>
    /// <typeparam name="TEffect">The effect type, an <see cref="EffectGroup"/> included.</typeparam>
    /// <typeparam name="TAction">The action type.</typeparam>
    /// <param name="action">
    /// The action to dispatch. A <see cref="StoreInitialized"/> is not dispatched: the store's own one, processed at Ready
    /// (<see cref="Origin.System"/>), runs an <c>Effect&lt;StoreInitialized&gt;</c> once, as in production.
    /// </param>
    /// <param name="configure">Runs before the store is built: adds the slices the effect reads or injects.</param>
    /// <param name="arrange">Runs on the built store, before the action: seeds state, drives a SliceStore through <see cref="TestStore.GetSlice{TSlice}"/>.</param>
    /// <param name="services">Registers fakes for the effect's dependencies.</param>
    /// <returns>What the effect dispatched, and the failures.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/> (thrown synchronously, SPEC §8.1).</exception>
    /// <exception cref="DuckyAssertionException">The store was not settled, or not disposed, within its real-time bound (on the task); a disposal failure that follows another failure carries it as <see cref="Exception.InnerException"/>.</exception>
    public static Task<EffectRun> Run<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEffect, TAction>(
        TAction action, Action<DuckyBuilder>? configure = null, Action<TestStore>? arrange = null, Action<IServiceCollection>? services = null)
        where TEffect : Effect
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(
            builder =>
            {
                builder.ThrowOnUnhandledAction = false;
                configure?.Invoke(builder);
                builder.AddEffect<TEffect>();
            },
            action,
            arrange,
            services);
    }
#pragma warning restore RS0026, VSTHRD200

    private static async Task<EffectRun> RunAsync(
        Action<DuckyBuilder> configure, object action, Action<TestStore>? arrange, Action<IServiceCollection>? services)
    {
        // Settled and DisposeAsync are bounded by TestStore.WallClockTimeout, which arrange can set (SPEC §15, INV-10 carve-out).
        var store = TestStore.Create(configure, services);
        ExceptionDispatchInfo? first = null;
        try
        {
            arrange?.Invoke(store);

            // Dispatch, not an awaited DispatchAsync: before Ready that task completes only once the action is reduced, so a
            // hung init would hang here, outside Settled's real-time bound. The action is queued when Dispatch returns
            // (INV-03), and Settled waits for it and the effect runs it starts. A StoreInitialized is not dispatched: Settled
            // starts init, and the store's own StoreInitialized (INV-13) runs the effect once; a second one would run it twice.
            if (action is not StoreInitialized)
            {
#pragma warning disable VSTHRD103 // justification: DispatchAsync is unbounded before Ready; Settled bounds the wait (comment above)
                store.Dispatch(action);
#pragma warning restore VSTHRD103
            }

            await store.Settled().ConfigureAwait(ConfigureAwaitOptions.None);
        }
#pragma warning disable CA1031 // justification: captured and rethrown below, after disposal, so a disposal failure cannot mask it
        catch (Exception exception)
#pragma warning restore CA1031
        {
            first = ExceptionDispatchInfo.Capture(exception);
        }

        // A run that ignores its token fails Settled and then disposal: the disposal failure carries the first one.
        try
        {
            await store.DisposeAsync().ConfigureAwait(false);
        }
        catch (DuckyAssertionException disposal) when (first is not null)
        {
            throw new DuckyAssertionException(disposal.Message, first.SourceException);
        }

        first?.Throw();

        // Read after disposal: nothing can append any more, so these snapshots are final.
        var dispatched = store.Processed.Where(context => context.Origin == Origin.Effect).Select(context => context.Action);
        return new([.. dispatched], store.Failures);
    }
}

/// <summary>What an <see cref="EffectTest"/> run recorded: immutable snapshots.</summary>
public sealed class EffectRun
{
    internal EffectRun(ImmutableList<object> dispatched, IReadOnlyList<object> failures)
    {
        Dispatched = dispatched;
        Failures = failures;
    }

    /// <summary>Gets every action the store's effects dispatched (<see cref="Origin.Effect"/>), in processing order.</summary>
    public IReadOnlyList<object> Dispatched { get; }

    /// <summary>Gets every <see cref="ReducerFailed"/> and <see cref="EffectFailed"/> the run processed, in order.</summary>
    public IReadOnlyList<object> Failures { get; }
}
