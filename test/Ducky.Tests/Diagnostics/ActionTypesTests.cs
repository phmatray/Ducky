using System.Text.Json;
using Ducky.Tests.ActionTypeFixtures;
using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §9 (action type names, DT-02), §5.8 ([ActionType]), §8.1 and §10 (unserializable values degrade to name only).
public sealed class ActionTypesTests
{
    private const string Fixtures = "Ducky.Tests.ActionTypeFixtures";

    // The name middleware see in ActionContext.ActionType: [ActionType] when present (not inherited), HydrateSlices is
    // @ducky/restore, otherwise Namespace.Name with generic arguments as Name<Arg> (named by the same rule) and nested
    // types as Outer.Inner. Computed once per action type per store.
    [Fact]
    public async Task ActionType_Names_Table()
    {
        (object Action, string Name)[] table =
        [
            (new Plain(), $"{Fixtures}.Plain"),
            (new Added("milk"), "todos/added"),
            (new Loaded<Item>(new Item()), $"{Fixtures}.Loaded<{Fixtures}.Item>"),
            (new Pair<int, string>(1, "one"), $"{Fixtures}.Pair<System.Int32, System.String>"),
            (new Loaded<Loaded<int[]>>(new([])), $"{Fixtures}.Loaded<{Fixtures}.Loaded<System.Int32[]>>"),
            (new Hit<int>(1), "cache/hit"),
            (new Outer.Inner(), $"{Fixtures}.Outer.Inner"),
            (new Loaded<Outer.Inner>(new Outer.Inner()), $"{Fixtures}.Loaded<{Fixtures}.Outer.Inner>"),
            (new Loaded<Loaded<int>[]>([]), $"{Fixtures}.Loaded<{Fixtures}.Loaded<System.Int32>[]>"),
            (new Loaded<Outer.Inner[,]>(new Outer.Inner[0, 0]), $"{Fixtures}.Loaded<{Fixtures}.Outer.Inner[,]>"),
            (new GenericOuter<int>.Inner(), $"{Fixtures}.GenericOuter<System.Int32>.Inner"),
            (new GenericOuter<int>.Nested<string>(), $"{Fixtures}.GenericOuter<System.Int32>.Nested<System.String>"),
            (new Named(), "base/named"),
            (new DerivedFromNamed(), $"{Fixtures}.DerivedFromNamed"),
            (new GlobalAction(), "GlobalAction"),
        ];

        var recorder = new Recorder("names", []);
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        foreach (var (action, _) in table)
        {
            await store.DispatchAsync(action);
        }

        store.Restore(new Dictionary<string, object> { ["count"] = new Count(1) }, Origin.Hydration);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        var names = recorder.Contexts.Where(c => c.Action is not StoreInitialized).DistinctBy(c => c.Action).ToList();
        names.Select(c => c.ActionType).ShouldBe([.. table.Select(row => row.Name), "@ducky/restore"]);

        // Once per type per store: a second action of a computed-name type gets the cached string itself.
        await store.DispatchAsync(new Loaded<Item>(new Item()));
        recorder.Contexts[^1].ActionType.ShouldBeSameAs(names[2].ActionType);
    }

    // DevTools and logs fall back to the action's name when its payload can't be serialized (§8.1): a missing type info,
    // a Type-carrying action (NotSupportedException) and a cyclic payload (JsonException). Each failing type is logged at
    // Debug (EventId 1070) once per store; a value-dependent failure doesn't stop the next value of the same type.
    // The name-only fallback itself is the consumer's job (DevToolsMiddleware, M8-02): until it lands, the recorder
    // rebuilds the (name, payload) pair and this test pins the DuckyJson contract per action type through a real
    // dispatch. The DevTools story extends it to assert on the real DevTools payload.
    [Fact]
    public async Task Unserializable_Action_DegradesToNameOnce()
    {
        var logger = new FakeLogger();
        List<(string Name, string? Payload)> seen = [];
        var recorder = new Recorder("devtools", []);
        var json = new JsonSerializerOptions { TypeInfoResolver = ProbeJson.Default };
        var store = new DuckyStore([new CountSlice()], logger, middleware: () => [recorder], json: json);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        recorder.OnAfter = c => seen.Add((c.ActionType, store.Json.ToNode(c.Action, c.Action.GetType())?.ToJsonString()));

        var cyclic = new CyclicProbe();
        cyclic.Next = cyclic;
        object[] actions = [new MissingProbe(1), new TypeProbe(typeof(int)), cyclic, new MissingProbe(2), new TypeProbe(typeof(string)), cyclic];
        foreach (var action in actions)
        {
            (await store.DispatchAsync(action)).ShouldBe(DispatchResult.Reduced);
        }

        (await store.DispatchAsync(new CyclicProbe())).ShouldBe(DispatchResult.Reduced);

        seen.ShouldBe(
        [
            ("probe/missing", null), ("probe/type", null), ("probe/cyclic", null),
            ("probe/missing", null), ("probe/type", null), ("probe/cyclic", null),
            ("probe/cyclic", """{"Next":null}"""),
        ]);

        var degraded = logger.Collector.GetSnapshot().Where(r => r.Id.Id == 1070).ToList();
        degraded.Select(r => r.Level).ShouldAllBe(level => level == LogLevel.Debug);
        degraded.Select(r => r.Message).ShouldBe(
        [
            $"{typeof(MissingProbe)} could not be serialized (no JsonTypeInfo, or the serializer threw); logged once per type per store",
            $"{typeof(TypeProbe)} could not be serialized (no JsonTypeInfo, or the serializer threw); logged once per type per store",
            $"{typeof(CyclicProbe)} could not be serialized (no JsonTypeInfo, or the serializer threw); logged once per type per store",
        ]);
        degraded[0].Exception.ShouldBeNull();
        degraded[1].Exception.ShouldBeOfType<NotSupportedException>();
        degraded[2].Exception.ShouldBeOfType<JsonException>();
    }
}
