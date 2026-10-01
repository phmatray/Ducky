using Ducky.Tests.SliceFixtures;
using Ducky.Tests.SnapshotFixtures;

namespace Ducky.Tests;

// SPEC §5.3 (StateSnapshot), §6.2; INV-07.
public sealed class StateSnapshotTests
{
    private const int CounterOrdinal = 0;
    private const int TodoOrdinal = 1;

    // Non-normative: DUCKY350 names the likely generated registration; matching is on the exact declared state type.
    [Fact]
    public void Snapshot_GetUnregisteredType_ThrowsDucky350()
    {
        var snapshot = new StateSnapshot(new Registry([new CounterSlice(), new PolymorphicSlice()]));

        ShouldThrowDucky350<Unregistered>(() => snapshot.Get<Unregistered>())
            .Message.ShouldContain("AddDuckyGenerated_Ducky_Tests()");
        ShouldThrowDucky350<Circle>(() => snapshot.Get<Circle>());
        ShouldThrowDucky350<Unregistered>(() => snapshot.WasRestored<Unregistered>());
        snapshot.TryGet<Unregistered>(out var missing).ShouldBeFalse();
        missing.ShouldBeNull();

        snapshot.Get<Shape>().ShouldBeOfType<Circle>();
        snapshot.TryGet<Counter>(out var counter).ShouldBeTrue();
        counter.ShouldBe(new Counter(0));
    }

    // Non-normative: a commit that changes no slot returns the same instance; one that does returns a new snapshot at
    // Version + 1 holding every change, and leaves the old snapshot unchanged.
    [Fact]
    public void Snapshot_CommitWithoutChange_ReturnsSameInstance()
    {
        var initial = Create();
        var counter = initial.Get<Counter>();
        var todos = initial.Get<Todos>();

        initial.Version.ShouldBe(0);
        initial.Commit([], Origin.Local).ShouldBeSameAs(initial);
        initial.Commit([(CounterOrdinal, counter), (TodoOrdinal, todos)], Origin.Local).ShouldBeSameAs(initial);

        var next = initial.Commit([(TodoOrdinal, todos), (CounterOrdinal, new Counter(1))], Origin.Local);

        next.ShouldNotBeSameAs(initial);
        next.Version.ShouldBe(1);
        next.Get<Counter>().ShouldBe(new Counter(1));
        next.Get<Todos>().ShouldBeSameAs(todos);
        initial.Get<Counter>().ShouldBeSameAs(counter);
        next.Commit([(TodoOrdinal, new Todos(1))], Origin.Local).Version.ShouldBe(2);

        var both = initial.Commit([(CounterOrdinal, new Counter(7)), (TodoOrdinal, new Todos(7))], Origin.Local);
        both.Get<Counter>().ShouldBe(new Counter(7));
        both.Get<Todos>().ShouldBe(new Todos(7));

        // The same ordinal twice in one commit: the last write wins.
        initial.Commit([(CounterOrdinal, new Counter(5)), (CounterOrdinal, counter)], Origin.Local)
            .Get<Counter>().ShouldBeSameAs(counter);
    }

    // Non-normative: only a Hydration-origin commit sets restored bits, on a copy; bits survive later commits.
    [Fact]
    public void Snapshot_WasRestored_BitsetCopiedOnlyOnHydration()
    {
        var initial = Create();
        var local = initial.Commit([(CounterOrdinal, new Counter(1))], Origin.CrossTab);

        local.WasRestored<Counter>().ShouldBeFalse();

        // Restoring an unchanged reference still publishes: the restored bit is a change.
        var todos = local.Get<Todos>();
        var hydrated = local.Commit([(TodoOrdinal, todos)], Origin.Hydration);

        hydrated.ShouldNotBeSameAs(local);
        hydrated.Version.ShouldBe(2);
        hydrated.WasRestored<Todos>().ShouldBeTrue();
        hydrated.WasRestored("todo").ShouldBeTrue();
        hydrated.WasRestored<Counter>().ShouldBeFalse();
        local.WasRestored<Todos>().ShouldBeFalse();
        initial.WasRestored("todo").ShouldBeFalse();

        hydrated.Commit([(TodoOrdinal, todos)], Origin.Hydration).ShouldBeSameAs(hydrated);
        var later = hydrated.Commit([(TodoOrdinal, new Todos(2)), (CounterOrdinal, new Counter(2))], Origin.Local);
        later.WasRestored<Todos>().ShouldBeTrue();
        later.WasRestored<Counter>().ShouldBeFalse();
        later.Get<Todos>().ShouldBe(new Todos(2));
        later.Get<Counter>().ShouldBe(new Counter(2));
        later.Commit([(CounterOrdinal, new Counter(3))], Origin.Hydration).WasRestored("counter").ShouldBeTrue();
    }

    // Non-normative: one Hydration commit can restore several slices.
    [Fact]
    public void Snapshot_HydrationCommit_RestoresEverySlotItChanges()
    {
        var restored = Create().Commit([(CounterOrdinal, new Counter(9)), (TodoOrdinal, new Todos(9))], Origin.Hydration);

        restored.WasRestored<Counter>().ShouldBeTrue();
        restored.WasRestored<Todos>().ShouldBeTrue();
        restored.Get<Counter>().ShouldBe(new Counter(9));
        restored.Get<Todos>().ShouldBe(new Todos(9));
    }

    // Non-normative: the restored bitset spans several 64-bit words; slice 64 is bit 0 of the second word, not slice 0.
    [Fact]
    public void Snapshot_WasRestored_Slice64UsesSecondWord()
    {
        var slices = new Slice[65];
        var stateType = typeof(Todos);
        for (var i = 0; i < slices.Length; i++)
        {
            slices[i] = (Slice)Activator.CreateInstance(typeof(BoxSlice<>).MakeGenericType(stateType), i)!;
            stateType = slices[i].StateType;
        }

        var initial = new StateSnapshot(new Registry(slices));
        var hydrated = initial.Commit([(64, initial.Get("box64"))], Origin.Hydration);

        hydrated.WasRestored("box64").ShouldBeTrue();
        hydrated.WasRestored("box0").ShouldBeFalse();
        initial.Commit([(0, initial.Get("box0"))], Origin.Hydration).WasRestored("box64").ShouldBeFalse();
    }

    // Non-normative: reads by key, in registration order.
    [Fact]
    public void Snapshot_GetByKey_ReadsSlotOrThrowsKeyNotFound()
    {
        var snapshot = Create();

        snapshot.Keys.ShouldBe(["counter", "todo"]);
        snapshot.Get("todo").ShouldBeSameAs(snapshot.Get<Todos>());
        Should.Throw<KeyNotFoundException>(() => snapshot.Get("missing"));
        Should.Throw<KeyNotFoundException>(() => snapshot.WasRestored("missing"));
    }

    private static StateSnapshot Create() => new(new Registry([new CounterSlice(), new TodoSlice()]));

    private static DuckyConfigurationException ShouldThrowDucky350<TState>(Action act)
    {
        var ex = Should.Throw<DuckyConfigurationException>(act);
        ex.Errors.Single().ShouldBe(DuckyErrors.UnregisteredState(typeof(TState)));
        ex.Errors.Single().Code.ShouldBe("DUCKY350");
        return ex;
    }
}
