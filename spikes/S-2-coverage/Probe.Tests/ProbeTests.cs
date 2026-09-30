using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Probe.Empty;
using Probe.Generator;

namespace Probe.Tests;

public sealed class ProbeTests
{
    [Fact]
    public async Task Async_CompletedAndPending_BothBranches()
    {
        (await AsyncShapes.AddAsync(Task.FromResult(1), 2)).ShouldBe(3);

        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sum = AsyncShapes.AddAsync(pending.Task, 2);
        pending.SetResult(5);
        (await sum).ShouldBe(7);
    }

    [Fact]
    public async Task AwaitUsing_CompletedAndPending_Disposes()
    {
        var done = new Resource(1, Task.CompletedTask, Task.CompletedTask);
        (await AsyncShapes.UseAsync(done)).ShouldBe(1);
        done.Disposed.ShouldBeTrue();

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gated = new Resource(2, ready.Task, disposal.Task);
        var use = AsyncShapes.UseAsync(gated);
        ready.SetResult();
        await Task.Yield();
        disposal.SetResult();
        (await use).ShouldBe(2);
        gated.Disposed.ShouldBeTrue();
    }

    // Completed tasks only: proves an await's suspension path needs no test for coverage (docs/spec/spikes.md).
    [Fact]
    public async Task Async_CompletedOnly_FullyCovered()
    {
        (await SyncOnlyAsyncShapes.AddAsync(Task.FromResult(1), 2)).ShouldBe(3);

        var done = new Resource(1, Task.CompletedTask, Task.CompletedTask);
        (await SyncOnlyAsyncShapes.UseAsync(done)).ShouldBe(1);
        done.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void Records_SynthesizedMembers()
    {
        var p = new Point(1, 2);
        p.Sum.ShouldBe(3);
        (p with { Y = 3 }).ShouldBe(new Point(1, 3));
        p.Equals(null).ShouldBeFalse();
        p.GetHashCode().ShouldBe(new Point(1, 2).GetHashCode());
        p.ToString().ShouldBe("Point { X = 1, Y = 2, Sum = 3 }");
        var (x, y) = p;
        (x + y).ShouldBe(3);

        Shape c = new Circle("c", 1);
        c.ShouldNotBe(new Shape("c"));
        c.ToString().ShouldBe("Circle { Name = c, Radius = 1 }");
        (c with { Name = "d" }).Name.ShouldBe("d");
    }

    [Fact]
    public void Locks_Lowered()
    {
        var counter = new Counter();
        counter.Increment().ShouldBe(1);
        counter.Decrement().ShouldBe(0);
    }

    [Fact]
    public void Switch_EveryArm()
    {
        Switches.Classify(3).ShouldBe(1);
        Switches.Classify(0).ShouldBe(0);
        Switches.Classify("ab").ShouldBe(2);
        Switches.Classify(null).ShouldBe(-1);
        Switches.Name(DayKind.Weekday).ShouldBe("weekday");
        Switches.Name(DayKind.Weekend).ShouldBe("weekend");
        Should.Throw<ArgumentOutOfRangeException>(() => Switches.Name((DayKind)2));
    }

    [Fact]
    public void Tombstone_Present()
    {
        var tombstone = typeof(Point).Assembly.GetType("Probe.IOldThing")!;
        tombstone.IsInterface.ShouldBeTrue();
        tombstone.GetMembers().ShouldBeEmpty();
        new MarkerAttribute().ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void Generator_ThroughDriver_PartialOnly()
    {
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("partial class A; class B; struct C;", cancellationToken: TestContext.Current.CancellationToken)]);
        var result = CSharpGeneratorDriver.Create(new GreeterGenerator())
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        result.Exception.ShouldBeNull();
        result.GeneratedSources.Select(s => s.HintName).ShouldBe(["AGreeter.g.cs"]);
    }
}
