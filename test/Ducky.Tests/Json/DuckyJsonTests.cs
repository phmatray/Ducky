using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ducky.Tests.JsonFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §5.1 (UseJson, RequireJsonTypeInfo), §5.2 (IStore.Json, IStore.Restore with JsonElement), §6.4 step 6, §10
// (DuckyJson); INV-23, INV-31.
public sealed class DuckyJsonTests
{
    private static IStore Resolve(Action<DuckyBuilder> configure, ILogger<DuckyStore>? logger = null)
    {
        var services = new ServiceCollection().AddSingleton(logger ?? NullLogger<DuckyStore>.Instance);
        services.AddDucky(configure);
        return services.BuildServiceProvider().GetRequiredService<IStore>();
    }

    private static DuckyConfigurationException ResolveFails(Action<DuckyBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddDucky(configure);
        using var provider = services.BuildServiceProvider();
        return Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void UseJson_Context_CopiesContextOptions()
    {
        // A context brings its [JsonSourceGenerationOptions]: camelCase and the AOT enum converter.
        var json = Resolve(d => d.AddSlice<NoteSlice>().UseJson(TestJson.Default)).Json;
        json.Options.ShouldNotBeSameAs(TestJson.Default.Options);
        json.Options.IsReadOnly.ShouldBeTrue();
        json.Options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        json.Options.TypeInfoResolver.ShouldBeSameAs(TestJson.Default);
        json.TrySerialize(new Status(Mood.Grumpy), typeof(Status), out var text).ShouldBeTrue();
        text.ShouldBe("""{"mood":"Grumpy"}""");

        // Any other resolver gets fresh options: Combine carries none of the context's.
        json = Resolve(d => d.UseJson(JsonTypeInfoResolver.Combine(TestJson.Default, PlainJson.Default))).Json;
        json.Options.PropertyNamingPolicy.ShouldBeNull();
        json.TrySerialize(new Status(Mood.Grumpy), typeof(Status), out text).ShouldBeTrue();
        text.ShouldBe("""{"Mood":1}""");

        // The last call wins, whichever overload made it.
        json = Resolve(d => d.UseJson(new JsonSerializerOptions { TypeInfoResolver = PlainJson.Default }).UseJson(TestJson.Default)).Json;
        json.Options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        json = Resolve(d => d.UseJson(TestJson.Default).UseJson(new JsonSerializerOptions { TypeInfoResolver = PlainJson.Default })).Json;
        json.Options.TypeInfoResolver.ShouldBeSameAs(PlainJson.Default);
        json.Options.PropertyNamingPolicy.ShouldBeNull();

        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(d => d.UseJson((IJsonTypeInfoResolver)null!))).ParamName.ShouldBe("resolver");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(d => d.UseJson((JsonSerializerOptions)null!))).ParamName.ShouldBe("options");
    }

    [Fact]
    public void UseJson_OptionsWithoutResolver_Ducky306_CallerInstanceNotFrozen()
    {
        // Reported with the other errors at first resolution, never from AddDucky; the required type isn't checked
        // against a resolver that doesn't exist.
        var options = new JsonSerializerOptions { WriteIndented = true };
        var exception = ResolveFails(d =>
        {
            d.Lifetime = ServiceLifetime.Transient;
            d.UseJson(options).RequireJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>");
        });
        exception.Errors.ShouldBe([DuckyErrors.TransientLifetime(), DuckyErrors.NullTypeInfoResolver()], ignoreOrder: true);
        options.IsReadOnly.ShouldBeFalse();

        // With a resolver, the store freezes a copy; the caller's instance, which the app may share, stays mutable.
        var shared = new JsonSerializerOptions { TypeInfoResolver = TestJson.Default, WriteIndented = true };
        var json = Resolve(d => d.UseJson(shared).RequireJsonTypeInfo(typeof(Note), "Persist<NoteSlice>")).Json;
        json.Options.ShouldNotBeSameAs(shared);
        json.Options.IsReadOnly.ShouldBeTrue();
        json.Options.WriteIndented.ShouldBeTrue();
        shared.IsReadOnly.ShouldBeFalse();
        shared.WriteIndented = false;
        json.Options.WriteIndented.ShouldBeTrue();
    }

    [Fact]
    public void RequireJsonTypeInfo_Missing_Ducky306()
    {
        // Covered types pass; a missing one is reported once, with the [JsonSerializable] line, among the other errors.
        var exception = ResolveFails(d => d
            .UseJson(TestJson.Default)
            .RequireJsonTypeInfo(typeof(Note), "Persist<NoteSlice>")
            .RequireJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>")
            .RequireJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>")
            .AddSlice<NoteSlice>()
            .AddValidation(_ => [DuckyErrors.TransientLifetime()]));
        var missing = DuckyErrors.MissingJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>");
        exception.Errors.ShouldBe([missing, DuckyErrors.TransientLifetime()], ignoreOrder: true);
        exception.Message.ShouldContain("[JsonSerializable(typeof(Ducky.Tests.JsonFixtures.Uncovered))]");

        // Without UseJson no type has type info.
        ResolveFails(d => d.RequireJsonTypeInfo(typeof(Note), "Prerender<NoteSlice>"))
            .Errors.ShouldBe([DuckyErrors.MissingJsonTypeInfo(typeof(Note), "Prerender<NoteSlice>")]);
        Resolve(d => d.UseJson(TestJson.Default).RequireJsonTypeInfo(typeof(Note), "Persist<NoteSlice>")).ShouldNotBeNull();

        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(d => d.RequireJsonTypeInfo(null!, "x"))).ParamName.ShouldBe("type");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(d => d.RequireJsonTypeInfo(typeof(Note), null!))).ParamName.ShouldBe("requiredBy");
    }

    [Fact]
    public void DuckyJson_CtorThrowingPayload_ReturnsFalse()
    {
        var logger = new FakeLogger();
        var json = new DuckyJson(TestJson.Default.Options, new SafeLogger(logger));

        json.TryDeserialize(Parse("""{"count":-1}"""), typeof(Picky), out var value, "picky").ShouldBeFalse();
        value.ShouldBeNull();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Id.Id.ShouldBe(1071);
        record.Message.ShouldContain("'picky'");
        record.Message.ShouldContain(typeof(Picky).FullName!);
        record.Exception.ShouldBeOfType<ArgumentOutOfRangeException>();

        // Nothing is cached for an exception: the next valid payload of the same type deserializes.
        json.TryDeserialize(Parse("""{"count":2}"""), typeof(Picky), out value).ShouldBeTrue();
        value.ShouldBeOfType<Picky>().Count.ShouldBe(2);

        // Malformed JSON and missing type info fail the same way, every time, each with its Warning.
        logger.Collector.Clear();
        json.TryDeserialize(Parse("""{"count":"many"}"""), typeof(Picky), out value, "picky").ShouldBeFalse();
        json.TryDeserialize(Parse("1"), typeof(Uncovered), out value, "uncovered").ShouldBeFalse();
        json.TryDeserialize(Parse("1"), typeof(Uncovered), out value).ShouldBeFalse();
        value.ShouldBeNull();
        var records = logger.Collector.GetSnapshot();
        records.Select(r => (r.Id.Id, r.Level)).ShouldBe([(1071, LogLevel.Warning), (1071, LogLevel.Warning), (1071, LogLevel.Warning)]);
        records[0].Exception.ShouldBeOfType<JsonException>();
        records[1].Exception.ShouldBeNull();
        records[1].Message.ShouldContain("'uncovered'");
        records[1].Message.ShouldContain(typeof(Uncovered).FullName!);
    }

    // Non-normative: missing type info is cached as unserializable; a serializer exception fails only that call. Each
    // failing type is logged at Debug once per store.
    [Fact]
    public void DuckyJson_Unserializable_FalseAndDebugOncePerType()
    {
        var logger = new FakeLogger();
        var json = new DuckyJson(TestJson.Default.Options, new SafeLogger(logger));

        json.TryGetTypeInfo(typeof(Note), out var typeInfo).ShouldBeTrue();
        typeInfo.ShouldNotBeNull().Type.ShouldBe(typeof(Note));
        json.TryGetTypeInfo(typeof(Uncovered), out typeInfo).ShouldBeFalse();
        typeInfo.ShouldBeNull();

        json.TrySerialize(new Uncovered(1), typeof(Uncovered), out var text).ShouldBeFalse();
        text.ShouldBeNull();
        json.ToNode(new Uncovered(1), typeof(Uncovered)).ShouldBeNull();

        json.TrySerialize(new Measure(double.NaN), typeof(Measure), out text).ShouldBeFalse();
        text.ShouldBeNull();
        json.ToNode(new Measure(double.PositiveInfinity), typeof(Measure)).ShouldBeNull();
        json.TrySerialize(new Measure(1.5), typeof(Measure), out text).ShouldBeTrue();
        text.ShouldBe("""{"value":1.5}""");
        json.ToNode(new Measure(2.5), typeof(Measure)).ShouldNotBeNull()["value"]!.GetValue<double>().ShouldBe(2.5);

        var records = logger.Collector.GetSnapshot();
        records.Select(r => (r.Id.Id, r.Level)).ShouldBe([(1070, LogLevel.Debug), (1070, LogLevel.Debug)]);
        records[0].Message.ShouldContain(typeof(Uncovered).FullName!);
        records[0].Exception.ShouldBeNull();
        records[1].Message.ShouldContain(typeof(Measure).FullName!);
        records[1].Exception.ShouldBeOfType<ArgumentException>();

        // Once per store: another store's DuckyJson logs again.
        new DuckyJson(TestJson.Default.Options, new SafeLogger(logger)).TrySerialize(new Uncovered(1), typeof(Uncovered), out _).ShouldBeFalse();
        logger.Collector.Count.ShouldBe(3);
    }

    // Non-normative: STJ throws from the type info lookup itself for a misconfigured type (a duplicate discriminator, a
    // property-name collision) or an invalid one (an open generic). That degrades like any other failure (INV-23).
    [Fact]
    public void DuckyJson_ThrowingTypeInfoLookup_DegradesNeverThrows()
    {
        var logger = new FakeLogger();
        var json = new DuckyJson(TestJson.Default.Options, new SafeLogger(logger));

        json.TryGetTypeInfo(typeof(Shape), out var typeInfo).ShouldBeFalse();
        typeInfo.ShouldBeNull();
        json.TryGetTypeInfo(typeof(List<>), out typeInfo).ShouldBeFalse();
        json.TrySerialize(new Circle(), typeof(Shape), out var text).ShouldBeFalse();
        text.ShouldBeNull();
        json.ToNode(new Circle(), typeof(Shape)).ShouldBeNull();
        json.TryDeserialize(Parse("""{"$type":"shape"}"""), typeof(Shape), out var value, "shape").ShouldBeFalse();
        value.ShouldBeNull();

        var records = logger.Collector.GetSnapshot();
        records.Select(r => (r.Id.Id, r.Level)).ShouldBe([(1070, LogLevel.Debug), (1071, LogLevel.Warning)]);
        records[0].Exception.ShouldBeOfType<InvalidOperationException>();
        records[1].Exception.ShouldBeOfType<InvalidOperationException>();
        records[1].Message.ShouldContain("'shape'");
        records[1].Message.ShouldContain(typeof(Shape).FullName!);

        // On the drainer, the other slices of the batch still restore (§6.4 step 6).
        var store = new DuckyStore([new NoteSlice(), new ShapeSlice()], NullLogger.Instance, json: TestJson.Default.Options);
        store.Restore(
            new Dictionary<string, object> { ["note"] = Parse("""{"text":"restored"}"""), ["shape"] = Parse("""{"$type":"shape"}""") },
            Origin.Hydration);
        store.State.Get<Note>().Text.ShouldBe("restored");
        store.State.Get<Shape>().ShouldBeOfType<Circle>();
        store.State.WasRestored<Shape>().ShouldBeFalse();

        // At first resolution, the lookup's exception is reported with the other errors (INV-31).
        var exception = ResolveFails(d => d
            .UseJson(TestJson.Default)
            .RequireJsonTypeInfo(typeof(Shape), "Persist<ShapeSlice>")
            .RequireJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>"));
        exception.Errors.ShouldBe([DuckyErrors.MissingJsonTypeInfo(typeof(Uncovered), "Persist<UncoveredSlice>")]);
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain($"JSON type info of {typeof(Shape).FullName} (required by Persist<ShapeSlice>) threw System.InvalidOperationException");
    }

    [Fact]
    public void Restore_JsonElementFromDisposedDocument_WhileOtherThreadDrains_Restores_Deterministic()
    {
        var store = new DuckyStore([new NoteSlice(), new PickySlice()], NullLogger.Instance, json: TestJson.Default.Options);
        var queued = false;

        // The outer restore's drain is active, so the JsonElement restore is only queued, and its document is disposed
        // before the drainer processes it: Restore cloned the element when it was called.
        store.Dispatcher.BeforeProcessHook = _ =>
        {
            store.Dispatcher.BeforeProcessHook = null;
            using (var document = JsonDocument.Parse("""{"text":"restored"}"""))
            {
                store.Restore(new Dictionary<string, object> { ["note"] = document.RootElement }, Origin.Hydration);
            }

            queued = store.Dispatcher.State.Get<Note>().Text == "initial";
        };
        store.Restore(new Dictionary<string, object> { ["picky"] = new Picky(3) }, Origin.Hydration);

        queued.ShouldBeTrue();
        store.State.Get<Note>().Text.ShouldBe("restored");
        store.State.WasRestored<Note>().ShouldBeTrue();
        store.State.Get<Picky>().Count.ShouldBe(3);
    }

    // Non-normative: TryRestore deserializes with the declared state type and passes the slice key; a slice whose value
    // fails keeps its state, is not marked restored, and the other slices of the batch still restore.
    [Fact]
    public void Restore_JsonElement_FailingSliceKeepsStateOthersRestore()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new NoteSlice(), new PickySlice()], logger, json: TestJson.Default.Options);
        store.Restore(new Dictionary<string, object> { ["picky"] = new Picky(5) }, Origin.CrossTab);

        store.Restore(
            new Dictionary<string, object> { ["note"] = Parse("""{"text":"restored"}"""), ["picky"] = Parse("""{"count":-1}""") },
            Origin.Hydration);

        store.State.Get<Note>().Text.ShouldBe("restored");
        store.State.Get<Picky>().Count.ShouldBe(5);
        store.State.WasRestored<Note>().ShouldBeTrue();
        store.State.WasRestored<Picky>().ShouldBeFalse();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1071);
        record.Message.ShouldContain("'picky'");
        record.Message.ShouldContain(typeof(Picky).FullName!);

        // JSON null is not a state either, and is never ignored silently.
        logger.Collector.Clear();
        store.Restore(new Dictionary<string, object> { ["note"] = Parse("null") }, Origin.Hydration);
        store.State.Get<Note>().Text.ShouldBe("restored");
        record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level).ShouldBe((1071, LogLevel.Warning));
        record.Message.ShouldContain("'note'");
        record.Message.ShouldContain(typeof(Note).FullName!);
    }

    // Non-normative: IStore.Json is the store's own DuckyJson, built from UseJson; a store without UseJson has no type
    // info at all, so every value degrades.
    [Fact]
    public void StoreJson_IsPerStore()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.UseJson(TestJson.Default));
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var json = first.ServiceProvider.GetRequiredService<IStore>().Json;

        json.ShouldBeSameAs(first.ServiceProvider.GetRequiredService<IStore>().Json);
        json.ShouldNotBeSameAs(second.ServiceProvider.GetRequiredService<IStore>().Json);
        json.TryGetTypeInfo(typeof(Note), out _).ShouldBeTrue();

        var bare = new DuckyStore([new NoteSlice()], NullLogger.Instance).Json;
        bare.Options.IsReadOnly.ShouldBeTrue();
        bare.TryGetTypeInfo(typeof(Note), out _).ShouldBeFalse();
    }
}
