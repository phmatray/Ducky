using System.Runtime.CompilerServices;

namespace Ducky;

/// <summary>
/// A slice with a Zustand-style facade: inject it (<c>AddSlice&lt;T&gt;()</c> registers it, resolved to the instance the
/// store owns), read <see cref="State"/>, and change it through methods that call <see cref="Set"/> or
/// <see cref="SetAsync"/>. It is still a slice, so its constructor can also react to actions with <c>On&lt;TAction&gt;</c>.
/// </summary>
/// <remarks>
/// A mutator runs when the store reduces the change, on the draining thread, against the latest state, so concurrent
/// changes all apply; it must be pure. Called from an effect run of the owning store, a change is scoped to that run like
/// <see cref="EffectContext.Dispatch(object)"/>. Work that needs a service or awaits belongs in an effect that injects the
/// SliceStore.
/// </remarks>
/// <typeparam name="TState">The state type; a reference type, since change detection is by reference.</typeparam>
public abstract class SliceStore<TState> : Slice<TState>
    where TState : class
{
    private DuckyStore? _store;

    /// <summary>Initializes a new instance of the <see cref="SliceStore{TState}"/> class.</summary>
    protected SliceStore() => On<SetState<TState>>((state, set) => set.Mutator(state));

    /// <summary>
    /// Gets the committed state: a point-in-time read, not a subscription. The first read starts the store's
    /// initialization. To follow changes in markup, select the state through the store.
    /// </summary>
    /// <exception cref="DuckyConfigurationException">
    /// The SliceStore was created with <c>new</c>, so no store attached it (DUCKY351), or the store could not create its
    /// effects and middleware (DUCKY353).
    /// </exception>
    public TState State => Store.State.Get<TState>();

    private DuckyStore Store => _store ?? throw new DuckyConfigurationException([DuckyErrors.SliceStoreNotAttached(GetType())]);

    internal override void Attach(DuckyStore store) => _store = store;

    /// <summary>
    /// Dispatches a change through the store's full pipeline, with the action type <c>{Key}/{name}</c>. The mutator runs
    /// when the change is reduced; returning the same instance commits nothing and notifies nobody.
    /// </summary>
    /// <param name="mutator">Returns the next state from the latest state; it must be pure.</param>
    /// <param name="name">The change's name, by default the calling member's name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mutator"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// The SliceStore was created with <c>new</c>, so no store attached it (DUCKY351), or the store could not create its
    /// effects and middleware (DUCKY353).
    /// </exception>
    protected void Set(Func<TState, TState> mutator, [CallerMemberName] string name = "") => Dispatch(mutator, name, null);

    /// <summary>
    /// Dispatches a change like <see cref="Set"/> and returns what happened to it. Never block on the task.
    /// </summary>
    /// <param name="mutator">Returns the next state from the latest state; it must be pure.</param>
    /// <param name="name">The change's name, by default the calling member's name.</param>
    /// <returns>
    /// The change's <see cref="DispatchResult"/>, once it was reduced: <see cref="DispatchResult.Disposed"/> once the
    /// store's disposal began, and <see cref="DispatchResult.Dropped"/> if the effect run that called it was superseded.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="mutator"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// Thrown synchronously: the SliceStore was created with <c>new</c>, so no store attached it (DUCKY351), or the store
    /// could not create its effects and middleware (DUCKY353).
    /// </exception>
    protected Task<DispatchResult> SetAsync(Func<TState, TState> mutator, [CallerMemberName] string name = "")
    {
        var completion = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(mutator, name, completion);
        return completion.Task;
    }

    private void Dispatch(Func<TState, TState> mutator, string name, TaskCompletionSource<DispatchResult>? completion)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        Store.Dispatcher.DispatchSet(new SetState<TState>($"{Key}/{name}", mutator), completion);
    }
}

// The action Set dispatches (SPEC §12); ActionTypes reads its {key}/{name} type from the instance (§9).
internal abstract record SetState(string ActionType);

internal sealed record SetState<TState>(string ActionType, Func<TState, TState> Mutator) : SetState(ActionType)
    where TState : class;
