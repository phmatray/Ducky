using Ducky.Testing.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Testing.Tests;

// SPEC §15 (TST-01, ADR-0039): EffectTest runs one action through an effect on a real TestStore and returns what it
// dispatched.
public sealed class EffectTestTests
{
    // Dispatched holds the effect's actions in order, after the effect finished, and not the triggering action. Run
    // disposes its store before it returns.
    [Fact]
    public async Task EffectTest_CollectsDispatched()
    {
        TestStore? store = null;

        var run = await EffectTest.Run(new FetchEffect(), new Fetch("a"), arrange: s => store = s);

        run.Dispatched.ShouldBe([new Fetched("a", 1), new Fetched("a", 2)]);
        run.Failures.ShouldBeEmpty();
        (await store!.DispatchAsync(new Fetch("late"))).ShouldBe(DispatchResult.Disposed);
    }

    // configure adds the slice the effect reads; arrange runs on the built store, before the action.
    [Fact]
    public async Task EffectTest_EffectReadingState_Works()
    {
        var run = await EffectTest.Run(new ReadTallyEffect(), new Fetch("t"), d => d.AddSlice<TallySlice>(), s => s.Seed(new Tally(5)));

        run.Dispatched.ShouldBe([new Fetched("t", 5)]);
        run.Failures.ShouldBeEmpty();
    }

    // No slice reduces Fetched: a result to assert on, not a failure. Strict mode is turned off before configure, which
    // can turn it back on, and then Failures holds the unhandled actions.
    [Fact]
    public async Task EffectTest_UnhandledResultAction_NotAFailure()
    {
        var lenient = await EffectTest.Run(new FetchEffect(), new Fetch("u"));
        var strict = await EffectTest.Run(new FetchEffect(), new Fetch("u"), d => d.ThrowOnUnhandledAction = true);

        lenient.Failures.ShouldBeEmpty();
        lenient.Dispatched.Count.ShouldBe(2);
        strict.Dispatched.ShouldBe(lenient.Dispatched);
        strict.Failures.Count.ShouldBe(2);
        strict.Failures.ShouldAllBe(f => f is ReducerFailed && ((ReducerFailed)f).Exception is UnhandledActionException);
    }

    // The given instance replaces the AddEffect<T>() that configure registers (as AddDuckyGenerated_{Asm}() would):
    // one runner, so the handler runs once.
    [Fact]
    public async Task EffectTest_ConfigureAlsoRegistersEffectType_HandlerRunsOnce()
    {
        var effect = new CountingEffect();

        var run = await EffectTest.Run(effect, new Fetch("c"), d => d.AddEffect<CountingEffect>());

        effect.Calls.ShouldBe(1);
        run.Dispatched.ShouldBe([new Fetched("c", 1)]);
    }

    // The type overload lets the store build the effect (an EffectGroup here), injecting the store-owned SliceStore and a
    // fake service; arrange drives the same SliceStore through GetSlice, and its Origin.Local action is not a result.
    [Fact]
    public async Task EffectTest_EffectInjectingSliceStore_Works()
    {
        var run = await EffectTest.Run<CartEffects, Fetch>(
            new Fetch("b"),
            d => d.AddSlice<CartStore>(),
            s => s.GetSlice<CartStore>().Add("a"),
            services => services.AddSingleton(new Prefix("item-")));

        // Fetched counts arrange's item, and the one SetState<Cart> (internal) follows it: the effect's Add, not arrange's.
        run.Dispatched.Count.ShouldBe(2);
        run.Dispatched[0].ShouldBe(new Fetched("item-b", 1));
        run.Dispatched[1].GetType().Name.ShouldBe("SetState`1");
        run.Dispatched[1].GetType().GetGenericArguments().ShouldBe([typeof(Cart)]);
        run.Failures.ShouldBeEmpty();
    }

    // Without the optional callbacks, the type overload builds a parameterless effect.
    [Fact]
    public async Task EffectTest_TypeOverload_DefaultsOptionalCallbacks()
    {
        var run = await EffectTest.Run<FetchEffect, Fetch>(new Fetch("d"));

        run.Dispatched.ShouldBe([new Fetched("d", 1), new Fetched("d", 2)]);
    }

    // A test double deriving from the real effect replaces AddEffect<RealEffect>() only when TEffect is the real type.
    [Fact]
    public async Task EffectTest_DerivedDoubleWithExplicitType_ReplacesRealEffect()
    {
        var run = await EffectTest.Run<FetchEffect, Fetch>(new FakeFetchEffect(), new Fetch("f"), d => d.AddEffect<FetchEffect>());

        run.Dispatched.ShouldBe([new Fetched("fake", 0)]);
    }

    // §12/§22.2's initial-load pattern: the store's own StoreInitialized (System origin) starts the effect, so Run does not
    // dispatch the given one too, and the injected service is called once.
    [Fact]
    public async Task EffectTest_StoreInitializedEffect_RunsOnce()
    {
        var calls = new LoadCalls();

        var run = await EffectTest.Run<InitLoadEffect, StoreInitialized>(
            new StoreInitialized(), services: services => services.AddSingleton(calls));

        calls.Count.ShouldBe(1);
        run.Dispatched.ShouldBe([new Fetched("init", 1)]);
        run.Failures.ShouldBeEmpty();
    }

    // A run that ignores its token fails Settled, then disposal: the disposal failure carries the first one as its inner
    // exception instead of masking it. A failure alone (arrange throws, disposal succeeds) propagates unwrapped.
    [Fact]
    public async Task EffectTest_SettledAndDisposeFail_KeepsFirstFailure()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var thrown = await Should.ThrowAsync<DuckyAssertionException>(
                () => EffectTest.Run(new HangEffect(gate), new Hang(), arrange: s => s.WallClockTimeout = TimeSpan.FromMilliseconds(200))
                    .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            thrown.Message.ShouldStartWith("The store was not disposed after 00:00:00.2000000");
            thrown.InnerException.ShouldBeOfType<DuckyAssertionException>().Message.ShouldStartWith("The store was not idle after 00:00:00.2000000");
        }
        finally
        {
            gate.TrySetResult();
        }

        var alone = await Should.ThrowAsync<InvalidOperationException>(
            () => EffectTest.Run(new FetchEffect(), new Fetch("x"), arrange: _ => throw new InvalidOperationException("arrange")));
        alone.Message.ShouldBe("arrange");
    }

    // SPEC §8.1/§7.4: a null argument throws synchronously, before a task or a store exists. Action lambdas, not
    // Func<Task>: Shouldly would wait on a returned task and accept a faulted one.
    [Fact]
    public void EffectTest_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => { _ = EffectTest.Run<FetchEffect, Fetch>(null!, new Fetch("n")); }).ParamName.ShouldBe("effect");
        Should.Throw<ArgumentNullException>(() => { _ = EffectTest.Run<FetchEffect, Fetch>(new FetchEffect(), null!); }).ParamName.ShouldBe("action");
        Should.Throw<ArgumentNullException>(() => { _ = EffectTest.Run<FetchEffect, Fetch>(null!); }).ParamName.ShouldBe("action");
    }
}
