namespace Ducky.Blazor;

/// <summary>A <see cref="DuckyComponent"/> that renders one slice's whole state through <see cref="State"/> (SPEC §11.1).</summary>
/// <typeparam name="TState">The slice's state type.</typeparam>
public abstract class DuckyComponent<TState> : DuckyComponent
    where TState : class
{
    private Selection<TState>? _state;

    /// <summary>
    /// Gets the slice's state through one whole-slice selection, created on first access or, at the latest, just before
    /// the first render closes registration, so markup may read it conditionally after the first render.
    /// </summary>
    /// <remarks>
    /// Read it only on the component's renderer (render, lifecycle methods, event handlers), like any component state.
    /// Each read is the value the change check compares against, so a read from another thread, such as a timer callback,
    /// can hide a change until the next commit. Marshal such work with <c>InvokeAsync</c>.
    /// </remarks>
    protected TState State => StateSelection().Value;

    // Created here, the selection takes today's value as its baseline: markup has not read it, so nothing on screen depends
    // on it. A selection that markup already read keeps the rendered value as its baseline, so no pending re-render is lost.
    private protected override void BeforeRegistrationCloses()
    {
        if (_state is null)
        {
            _ = StateSelection().Value;
        }
    }

    private Selection<TState> StateSelection() => _state ??= Select(static (TState state) => state);
}
