using Ducky.Tests.SelectorFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.2 (StoreExtensions.Select), §6.9; INV-21.
public sealed class StoreExtensionsTests
{
    [Fact]
    public async Task TypedSelect_FastPath_SkipsProjectorWhenSlotUnchanged()
    {
        await using var store = new DuckyStore([new ASlice(), new ESlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var calls = new Calls();
        List<Sum> changes = [];

        using var selection = store.Select((A a) => calls.Hit(a.Value), changes.Add);
        var first = selection.Value;
        first.ShouldBe(new Sum(0));
        calls.Count.ShouldBe(1, "Value reuses the registration read's result");

        // A commit to another slot: the drainer re-evaluates, but A's reference is unchanged.
        store.Dispatch(new SetE(1));
        selection.Value.ShouldBeSameAs(first);
        calls.Count.ShouldBe(1);
        changes.ShouldBeEmpty();

        // An equal but new A is a new reference: the projector reruns, and the comparer dedupes the equal result.
        store.Dispatch(new SetA(0));
        calls.Count.ShouldBe(2);
        changes.ShouldBeEmpty();
        var second = selection.Value;
        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
        calls.Count.ShouldBe(2);

        store.Dispatch(new SetA(3));
        changes.ShouldBe([new Sum(3)]);
        selection.Value.ShouldBe(new Sum(3));
        calls.Count.ShouldBe(3);

        // Each Select has an entry of its own, also without onChange.
        using var other = store.Select<A, Sum>(a => calls.Hit(a.Value));
        var fromOther = other.Value;
        fromOther.ShouldBe(new Sum(3));
        other.Value.ShouldBeSameAs(fromOther);
        calls.Count.ShouldBe(4);
    }

    // Non-normative: the arguments are checked when Select is called.
    [Fact]
    public async Task TypedSelect_NullArgument_Throws()
    {
        await using var store = new DuckyStore([new ASlice()], NullLogger.Instance);

        Should.Throw<ArgumentNullException>(() => StoreExtensions.Select<A, int>(null!, a => a.Value)).ParamName.ShouldBe("store");
        Should.Throw<ArgumentNullException>(() => store.Select<A, int>(null!)).ParamName.ShouldBe("selector");
    }
}
