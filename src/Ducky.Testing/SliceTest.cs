namespace Ducky.Testing;

/// <summary>Starts a given/when/then test of one slice, run on a real <see cref="TestStore"/>.</summary>
public static class SliceTest
{
    /// <summary>Starts a test of <typeparamref name="TSlice"/>.</summary>
    /// <typeparam name="TSlice">The slice under test.</typeparam>
    /// <typeparam name="TState">Its state type.</typeparam>
    /// <returns>The test, to continue with <see cref="SliceTest{TState}.Given"/> or <see cref="SliceTest{TState}.When"/>.</returns>
    public static SliceTest<TState> For<TSlice, TState>()
        where TSlice : Slice<TState>, new()
        where TState : class => new(builder => builder.AddSlice<TSlice>());
}

/// <summary>
/// A given/when/then test of one slice. Each <c>Then</c> builds a <see cref="TestStore"/>, lets it initialize, seeds the
/// given state (if any) through <see cref="TestStore.Seed{TState}"/>, dispatches every action with
/// <see cref="TestStore.Dispatch"/> (reduced before it returns) and asserts on the resulting state.
/// </summary>
/// <typeparam name="TState">The state type of the slice under test.</typeparam>
public sealed class SliceTest<TState>
    where TState : class
{
    private readonly Action<DuckyBuilder> _configure;
    private TState? _given;
    private object[] _actions = [];

    internal SliceTest(Action<DuckyBuilder> configure) => _configure = configure;

    /// <summary>Sets the state the slice starts from; without it, the slice starts from its state after init.</summary>
    /// <param name="state">The state.</param>
    /// <returns>This test.</returns>
    public SliceTest<TState> Given(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _given = state;
        return this;
    }

    /// <summary>Sets the actions to dispatch, in order.</summary>
    /// <param name="actions">The actions.</param>
    /// <returns>This test.</returns>
    public SliceTest<TState> When(params object[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = [.. actions];
        return this;
    }

    /// <summary>Asserts that the resulting state equals <paramref name="expected"/> (<see cref="object.Equals(object, object)"/>).</summary>
    /// <param name="expected">The expected state.</param>
    /// <exception cref="DuckyAssertionException">The states differ, or an action was not processed.</exception>
    public void Then(TState expected)
    {
        var actual = Run().Actual;
        if (!Equals(expected, actual))
        {
            throw new DuckyAssertionException($"Expected the state {expected}, but it was {actual}.");
        }
    }

    /// <summary>Runs <paramref name="assert"/> on the resulting state.</summary>
    /// <param name="assert">The assertions, in any test framework.</param>
    /// <exception cref="DuckyAssertionException">An action was not processed.</exception>
    public void Then(Action<TState> assert)
    {
        ArgumentNullException.ThrowIfNull(assert);
        assert(Run().Actual);
    }

    /// <summary>Asserts that the resulting state is the given state itself (reference equality); without <see cref="Given"/>, the state after init.</summary>
    /// <exception cref="DuckyAssertionException">The slice returned another instance, or an action was not processed.</exception>
    public void ThenUnchanged()
    {
        var (start, actual) = Run();
        if (!ReferenceEquals(start, actual))
        {
            throw new DuckyAssertionException($"Expected the state to stay the same instance, {start}, but it became {actual}.");
        }
    }

    private (TState Start, TState Actual) Run()
    {
        using var store = TestStore.Create(_configure);

        // Reading State starts init, which completes inline (the recording middleware is synchronous, §6.7), so StoreInitialized
        // is reduced before the seed and a slice reducing it never rewrites the given state. Without Given nothing is seeded.
        var start = store.State.Get<TState>();
        if (_given is not null)
        {
            start = _given;
            store.Seed(start);
        }

        foreach (var action in _actions)
        {
            store.Dispatch(action);
        }

        var processed = store.Processed;
        var missing = Array.Find(_actions, action => !processed.Any(context => ReferenceEquals(context.Action, action)));
        if (missing is not null)
        {
            throw new DuckyAssertionException($"{missing} is missing from Processed: Dispatch returned before reducing it.");
        }

        return (start, store.State.Get<TState>());
    }
}
