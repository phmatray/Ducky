using Ducky.Testing.Tests.Fixtures;

namespace Ducky.Testing.Tests;

// SPEC §15 (TST-01, ADR-0039): SliceTest runs a slice on a real TestStore, synchronously.
public sealed class SliceTestTests
{
    // ThenUnchanged is reference equality with the given state (the initial state without Given): an equal copy fails.
    [Fact]
    public void SliceTest_ThenUnchanged_ReferenceEquals()
    {
        var given = new Tally(3);

        SliceTest.For<TallySlice, Tally>().Given(given).When(new Keep()).ThenUnchanged();
        SliceTest.For<TallySlice, Tally>().When(new Keep(), new Keep()).ThenUnchanged();
        SliceTest.For<TallySlice, Tally>().Given(given).When(new Keep()).Then(s => s.ShouldBeSameAs(given));

        var copied = Should.Throw<DuckyAssertionException>(
            () => SliceTest.For<TallySlice, Tally>().Given(given).When(new Copy()).ThenUnchanged());
        copied.Message.ShouldContain("Tally { Value = 3 }");
        Should.Throw<DuckyAssertionException>(() => SliceTest.For<TallySlice, Tally>().When(new Copy()).ThenUnchanged());
    }

    // Then(expected) compares with Equals after every action, in order, and names both values when they differ.
    [Fact]
    public void SliceTest_Then_ComparesWithEquals()
    {
        SliceTest.For<TallySlice, Tally>().Given(new Tally(3)).When(new Add(2), new Add(1)).Then(new Tally(6));
        SliceTest.For<TallySlice, Tally>().When(new Add(4)).Then(new Tally(4));
        SliceTest.For<TallySlice, Tally>().Given(new Tally(3)).Then(new Tally(3));

        var mismatch = Should.Throw<DuckyAssertionException>(
            () => SliceTest.For<TallySlice, Tally>().Given(new Tally(3)).When(new Add(2)).Then(new Tally(9)));
        mismatch.Message.ShouldContain("Tally { Value = 9 }");
        mismatch.Message.ShouldContain("Tally { Value = 5 }");
    }

    // A Dispatch that is not reduced inline (here init never completes, §6.7) leaves its action out of Processed: every
    // Then reports it instead of asserting on a state that never saw the action.
    [Fact]
    public void SliceTest_ActionMissingFromProcessed_Throws()
    {
        var test = new SliceTest<Tally>(d => d.AddSlice<TallySlice>().Use<PendingInitMiddleware>()).When(new Keep(), new Add(1));
        var called = false;

        Should.Throw<DuckyAssertionException>(() => test.Then(new Tally(0))).Message.ShouldContain("Keep { }");
        Should.Throw<DuckyAssertionException>(() => test.Then(_ => called = true)).Message.ShouldContain("Processed");
        Should.Throw<DuckyAssertionException>(test.ThenUnchanged);
        called.ShouldBeFalse();
    }

    // An action no slice handles leaves the state as it was: TestStore records a ReducerFailed (strict mode), but SliceTest
    // only fails when an action is missing from Processed (§15), so ThenUnchanged and Then(unchanged) pass.
    [Fact]
    public void SliceTest_UnrelatedAction_ThenUnchanged()
    {
        var given = new Tally(1);

        SliceTest.For<TallySlice, Tally>().Given(given).When(new UnrelatedAction()).ThenUnchanged();
        SliceTest.For<TallySlice, Tally>().Given(given).When(new Add(1), new UnrelatedAction()).Then(new Tally(2));
        SliceTest.For<TallySlice, Tally>().When(new UnrelatedAction()).ThenUnchanged();
    }

    // The given state is seeded after init: a slice reducing StoreInitialized never rewrites it, so ThenUnchanged holds and
    // Then sees only the When reducers (a broken Add whose expected result StoreInitialized would produce fails). Without
    // Given the test starts from the state after init.
    [Fact]
    public void SliceTest_SliceReducingStoreInitialized_GivenSeededAfterInit()
    {
        var given = new Tally(5);

        SliceTest.For<InitTallySlice, Tally>().Given(given).ThenUnchanged();
        SliceTest.For<InitTallySlice, Tally>().Given(given).When(new Keep()).ThenUnchanged();
        Should.Throw<DuckyAssertionException>(
            () => SliceTest.For<InitTallySlice, Tally>().Given(new Tally(1)).When(new Add(100)).Then(new Tally(101)));
        SliceTest.For<InitTallySlice, Tally>().Then(new Tally(100));
        SliceTest.For<InitTallySlice, Tally>().When(new Keep()).ThenUnchanged();
    }

    // Null arguments are programmer errors, thrown up front (§8.1); Given(null) is not "no Given".
    [Fact]
    public void SliceTest_NullArguments_Throw()
    {
        var test = SliceTest.For<TallySlice, Tally>();

        Should.Throw<ArgumentNullException>(() => test.Given(null!)).ParamName.ShouldBe("state");
        Should.Throw<ArgumentNullException>(() => test.When(null!)).ParamName.ShouldBe("actions");
        Should.Throw<ArgumentNullException>(() => test.Then((Action<Tally>)null!)).ParamName.ShouldBe("assert");
    }
}
