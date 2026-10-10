using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Testing;

/// <summary>
/// A real store for tests, built on Ducky's public API: it runs on a <see cref="FakeTimeProvider"/>, treats unhandled
/// actions as failures, and records every processed action and every failure.
/// </summary>
public sealed partial class TestStore : IStore
{
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _scope;
    private readonly Recorder _recorder;
    private readonly IStore _store;

    private TestStore(ServiceProvider services, Recorder recorder, FakeTimeProvider time)
    {
        _services = services;
        _scope = services.CreateAsyncScope();
        _recorder = recorder;
        _store = _scope.ServiceProvider.GetRequiredService<IStore>();
        Time = time;
    }

    /// <summary>Gets the store's clock; advance it to fire effect delays and timeouts.</summary>
    public FakeTimeProvider Time { get; }

    /// <summary>
    /// Gets every action that reached <c>AfterReduce</c> (reduced, or failed in <c>BeforeReduce</c> or a reducer), in
    /// processing order: an immutable snapshot, safe to enumerate while the store keeps processing.
    /// </summary>
    public IReadOnlyList<ActionContext> Processed => _recorder.Processed;

    /// <summary>
    /// Gets every processed <see cref="ReducerFailed"/>, <see cref="EffectFailed"/> and action whose type was given to
    /// <see cref="Create"/> as a failure type, in processing order: an immutable snapshot.
    /// </summary>
    public IReadOnlyList<object> Failures => _recorder.Failures;

    /// <inheritdoc/>
    public StateSnapshot State => _store.State;

    /// <inheritdoc/>
    public StateSnapshot InitialState => _store.InitialState;

    /// <inheritdoc/>
    public IReadOnlyList<Slice> Slices => _store.Slices;

    /// <inheritdoc/>
    public DuckyJson Json => _store.Json;

    /// <summary>
    /// Builds a store in its own container. The container's <see cref="TimeProvider"/> is always <see cref="Time"/>,
    /// replacing one <paramref name="services"/> registers, and <see cref="DuckyBuilder.ThrowOnUnhandledAction"/> is set
    /// before <paramref name="configure"/> runs, so a test can opt out.
    /// </summary>
    /// <param name="configure">Adds slices, effects and middleware, and sets options.</param>
    /// <param name="services">Registers the services the store's effects and middleware need (fakes).</param>
    /// <param name="failureTypes">Action types recorded in <see cref="Failures"/> besides <see cref="ReducerFailed"/> and <see cref="EffectFailed"/>.</param>
    /// <returns>The store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> or <paramref name="failureTypes"/> is null.</exception>
    public static TestStore Create(Action<DuckyBuilder> configure, Action<IServiceCollection>? services = null, params Type[] failureTypes)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(failureTypes);
        var recorder = new Recorder(failureTypes);
        var time = new FakeTimeProvider();
        var collection = new ServiceCollection();
        collection.AddSingleton(recorder);
        collection.AddDucky(builder =>
        {
            builder.ThrowOnUnhandledAction = true;
            builder.Use<RecordingMiddleware>();
            configure(builder);
        });
        services?.Invoke(collection);

        // After the callback: a TimeProvider it registers would otherwise become the store's clock and orphan Time.
        collection.RemoveAll<TimeProvider>();
        collection.AddSingleton<TimeProvider>(time);
        return new(collection.BuildServiceProvider(), recorder, time);
    }

    /// <summary>
    /// Restores <paramref name="state"/> into the slice whose declared state type is exactly <typeparamref name="TState"/>,
    /// with <see cref="Origin.Hydration"/>, so <see cref="StateSnapshot.WasRestored{TState}"/> becomes true.
    /// </summary>
    /// <typeparam name="TState">The declared state type of a registered slice.</typeparam>
    /// <param name="state">The state.</param>
    /// <returns>This store.</returns>
    /// <exception cref="DuckyConfigurationException">No slice declares <typeparamref name="TState"/> (DUCKY350).</exception>
    public TestStore Seed<TState>(TState state)
        where TState : class
    {
        _ = InitialState.Get<TState>(); // DUCKY350 when no slice declares TState

        // Stryker disable once Linq : equivalent, InitialState.Get<TState>() above already threw DUCKY350 when no slice matches
        var key = Slices.Single(slice => slice.StateType == typeof(TState)).Key;
        _store.Restore(new Dictionary<string, object> { [key] = state }, Origin.Hydration);
        return this;
    }

    /// <summary>Gets the store-owned instance of a slice or SliceStore, so a test can call its methods.</summary>
    /// <typeparam name="TSlice">A registered slice type.</typeparam>
    /// <returns>The instance the store attached.</returns>
    public TSlice GetSlice<TSlice>()
        where TSlice : Slice => _scope.ServiceProvider.GetRequiredService<TSlice>();

    /// <summary>
    /// Waits until the store is idle (<see cref="WhenIdleAsync"/>), within a real-time bound. Like
    /// <see cref="WhenIdleAsync"/>, it does not wait for <see cref="Effect.LongRunning"/> effects.
    /// </summary>
    /// <param name="timeout">The real-time bound; <see cref="WallClockTimeout"/> when null.</param>
    /// <returns>A task that completes once the store is idle.</returns>
    /// <exception cref="DuckyAssertionException">The store was not idle within <paramref name="timeout"/> (on the task).</exception>
    /// <exception cref="InvalidOperationException">Called from a run of one of this store's effects that is not long-running.</exception>
#pragma warning disable VSTHRD200 // justification: the spec's name (SPEC §15), read as an assertion: await store.Settled()
    public Task Settled(TimeSpan? timeout = null)
    {
        var bound = timeout ?? WallClockTimeout;
        return BoundAsync(
            _store.WhenIdleAsync(),
            bound,
            () => $"The store was not idle after {bound} of real time: an effect run or an action it causes is still pending.");
    }
#pragma warning restore VSTHRD200

    /// <inheritdoc/>
    public void Dispatch(object action) => _store.Dispatch(action);

    /// <inheritdoc/>
    public Task<DispatchResult> DispatchAsync(object action) => _store.DispatchAsync(action);

#pragma warning disable CA1716 // justification: forwards IStore.Select (SPEC §5.2, §15)
    /// <inheritdoc/>
    public Selection<T> Select<T>(Func<StateSnapshot, T> selector, Action<T>? onChange = null, IEqualityComparer<T>? comparer = null) =>
        _store.Select(selector, onChange, comparer);
#pragma warning restore CA1716

    /// <inheritdoc/>
    public void Restore(IReadOnlyDictionary<string, object> values, Origin origin) => _store.Restore(values, origin);

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => _store.InitializeAsync(cancellationToken);

    /// <inheritdoc/>
    public Task WhenIdleAsync(CancellationToken cancellationToken = default) => _store.WhenIdleAsync(cancellationToken);

    // A real-time bound over a task whose own bounds run on Time, which nobody advances while the test waits.
    private static async Task BoundAsync(Task task, TimeSpan timeout, Func<string> message)
    {
        try
        {
            await task.WaitAsync(timeout, TimeProvider.System).ConfigureAwait(ConfigureAwaitOptions.None);
        }
        catch (TimeoutException exception)
        {
            throw new DuckyAssertionException(message(), exception);
        }
    }
}
