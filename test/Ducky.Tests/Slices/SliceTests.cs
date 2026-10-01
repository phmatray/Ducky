using Ducky.Tests.SliceFixtures;

namespace Ducky.Tests;

// SPEC §5.3, §6.1; ADR-0008.
public sealed class SliceTests
{
    [Fact]
    public void DuplicateHandler_ThrowsAtConstruction() =>
        ShouldThrowConfiguration(
            () => _ = new DuplicateHandlerSlice(), DuckyErrors.DuplicateHandler(typeof(DuplicateHandlerSlice), typeof(Increment)), "DUCKY308");

    [Fact]
    public void OnAfterFreeze_Throws()
    {
        var slice = new CounterSlice();
        slice.Freeze();

        Should.Throw<InvalidOperationException>(slice.Register<Reset>)
            .Message.ShouldBe(
                "Slice Ducky.Tests.SliceFixtures.CounterSlice registers On<Ducky.Tests.SliceFixtures.Reset> after the store froze it. "
                + "Register every handler in the slice constructor.");
        slice.CanHandle(typeof(Reset)).ShouldBeFalse();
    }

    [Fact]
    public void ExactTypeMatching_DerivedActionNotHandledByBaseHandler()
    {
        var slice = new CounterSlice();
        slice.Freeze();
        var zero = new Counter(0);

        slice.CanHandle(typeof(Added)).ShouldBeTrue();
        slice.CanHandle(typeof(AddedTwice)).ShouldBeFalse();
        slice.TryReduce(zero, new AddedTwice(5), out var next).ShouldBeFalse();
        next.ShouldBeSameAs(zero);
        slice.TryReduce(zero, new Added(5), out next).ShouldBeTrue();
        next.ShouldBe(new Counter(5));
    }

    // Non-normative: DUCKY307 for abstract, interface and object handlers.
    [Fact]
    public void NonConcreteHandler_ThrowsAtConstruction()
    {
        var slice = new CounterSlice();

        ShouldThrowDucky307(slice.Register<AbstractAction>, typeof(AbstractAction));
        ShouldThrowDucky307(slice.Register<IAction>, typeof(IAction));
        ShouldThrowDucky307(slice.Register<object>, typeof(object));

        // C# spelling Outer.Inner, not Type.ToString()'s Outer+Inner.
        ShouldThrowDucky307(slice.Register<Outer.NestedAction>, typeof(Outer.NestedAction))
            .Message.ShouldStartWith("DUCKY307: Ducky.Tests.SliceFixtures.CounterSlice calls On<Ducky.Tests.SliceFixtures.Outer.NestedAction>()");
    }

    // Non-normative: both On overloads reduce; an unhandled action leaves the state alone.
    [Fact]
    public void TryReduce_BothOverloads_ApplyTheirHandler()
    {
        var slice = new CounterSlice();
        slice.Freeze();

        slice.TryReduce(new Counter(3), new Increment(), out var next).ShouldBeTrue();
        next.ShouldBe(new Counter(4));
        slice.TryReduce(new Counter(3), new Added(2), out next).ShouldBeTrue();
        next.ShouldBe(new Counter(5));
        slice.TryReduce(new Counter(3), "unhandled", out next).ShouldBeFalse();
        slice.CanHandle(typeof(string)).ShouldBeFalse();
    }

    [Fact]
    public void On_NullReducer_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new NullReducerSlice(withAction: true)).ParamName.ShouldBe("reducer");
        Should.Throw<ArgumentNullException>(() => new NullReducerSlice(withAction: false)).ParamName.ShouldBe("reducer");
    }

    // Non-normative: Key, StateType and InitialState.
    [Fact]
    public void Key_DefaultsToSliceKeyFromType_Cached()
    {
        Slice slice = new CounterSlice();

        slice.Key.ShouldBe("counter");
        slice.Key.ShouldBeSameAs(slice.Key);
        new KeyedSlice().Key.ShouldBe("custom");
    }

    [Fact]
    public void StateType_IsDeclaredStateType() =>
        new PolymorphicSlice().StateType.ShouldBe(typeof(Shape));

    [Fact]
    public void InitialState_ReadOnce()
    {
        var slice = new CounterSlice();

        slice.InitialState.ShouldBeSameAs(slice.InitialState);
        slice.InitialReads.ShouldBe(1);
    }

    private static DuckyConfigurationException ShouldThrowDucky307(Action register, Type actionType) =>
        ShouldThrowConfiguration(register, DuckyErrors.NonConcreteHandlerType(typeof(CounterSlice), actionType), "DUCKY307");

    private static DuckyConfigurationException ShouldThrowConfiguration(Action act, DuckyError expected, string code)
    {
        var ex = Should.Throw<DuckyConfigurationException>(act);
        ex.Errors.Single().ShouldBe(expected);
        ex.Errors.Single().Code.ShouldBe(code);
        ex.Message.ShouldBe(DuckyErrors.Format(expected));
        return ex;
    }
}
