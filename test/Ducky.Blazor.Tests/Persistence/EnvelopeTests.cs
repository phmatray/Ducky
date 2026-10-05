using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CsCheck;
using Ducky.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 (Envelope, the shared EnvelopeReader), §10 (state through the declared type's JsonTypeInfo, context
// options), ADR-0033 (1.x data discarded), ADR-0047 (a newer version is not found); INV-14.
public sealed class EnvelopeTests
{
    private const string Key = "ducky:tally";

    private static readonly DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<int, Func<JsonNode, JsonNode>> _none = [];

    private sealed record Harness(ServiceProvider Provider, IStore Store, EnvelopeReader Reader, FakeLogCollector Logs) : IDisposable
    {
        public string Payload(object state, Type declaredType)
        {
            Store.Json.TrySerialize(state, declaredType, out var payload).ShouldBeTrue();
            return payload;
        }

        public IReadOnlyList<(int Id, LogLevel Level)> TakeLogs() =>
            [.. Logs.GetSnapshot(clear: true).Select(record => (record.Id.Id, record.Level))];

        public void Dispose() => Provider.Dispose();
    }

    private static Harness Build(JsonSerializerContext? context = null)
    {
        var provider = new ServiceCollection()
            .AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug))
            .AddDucky(d => d.AddBlazor().AddSlice<TallySlice>().AddSlice<ShapeSlice>().AddSlice<StatusSlice>().UseJson(context ?? EnvelopeJson.Default))
            .BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        var reader = new EnvelopeReader(store.Json, provider.GetRequiredService<ILogger<EnvelopeReader>>());
        var harness = new Harness(provider, store, reader, provider.GetFakeLogCollector());
        harness.TakeLogs();
        return harness;
    }

    private static JsonObject Rename(JsonNode node, string from, string to)
    {
        var obj = node.AsObject();
        var value = obj[from];
        obj.Remove(from);
        obj[to] = value;
        return obj;
    }

    [Fact]
    public void Persist_Envelope_Migrations_Ttl_NewerVersionIgnored()
    {
        using var h = Build();
        var steps = new Dictionary<int, Func<JsonNode, JsonNode>>
        {
            [1] = node => Rename(node, "count", "value"),
            [2] = node =>
            {
                node["value"] = node["value"]!.GetValue<int>() * 10;
                return node;
            },
        };

        // The envelope: {v, at, s} with s written verbatim, and no type name.
        var envelope = EnvelopeWriter.Write("""{"count":3}""", 1, _now.ToOffset(TimeSpan.FromHours(2)));
        envelope.ShouldBe("""{"v":1,"at":"2026-09-30T12:00:00Z","s":{"count":3}}""");

        // v < Version: the steps run in order over JsonNode, up to Version.
        h.Reader.TryRead(envelope, Key, typeof(Tally), 3, steps, null, _now, out var state).ShouldBeTrue();
        state.ShouldBe(new Tally(30));
        EnvelopeWriter.Write("""{"value":3}""", 2, _now).ShouldBe("""{"v":2,"at":"2026-09-30T12:00:00Z","s":{"value":3}}""");
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":3}""", 2, _now), Key, typeof(Tally), 3, steps, null, _now, out state).ShouldBeTrue();
        state.ShouldBe(new Tally(30));
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":3}""", 3, _now), Key, typeof(Tally), 3, steps, null, _now, out state).ShouldBeTrue();
        state.ShouldBe(new Tally(3));
        h.TakeLogs().ShouldBeEmpty();

        // MaxAge: an envelope exactly MaxAge old is kept; one tick older is discarded (Debug).
        var maxAge = TimeSpan.FromHours(1);
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":4}""", 3, _now - maxAge), Key, typeof(Tally), 3, steps, maxAge, _now, out state).ShouldBeTrue();
        state.ShouldBe(new Tally(4));
        h.TakeLogs().ShouldBeEmpty();
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":4}""", 3, _now - maxAge - TimeSpan.FromTicks(1)), Key, typeof(Tally), 3, steps, maxAge, _now, out state).ShouldBeFalse();
        state.ShouldBeNull();
        var expired = h.Logs.GetSnapshot(clear: true).ShouldHaveSingleItem();
        (expired.Id.Id, expired.Level).ShouldBe((2032, LogLevel.Debug));
        expired.Message.ShouldContain($"'{Key}'");

        // v > Version (a rollback, a stale tab): not found, Warning, nothing deserialized.
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":5}""", 4, _now), Key, typeof(Tally), 3, steps, null, _now, out state).ShouldBeFalse();
        state.ShouldBeNull();
        var newer = h.Logs.GetSnapshot(clear: true).ShouldHaveSingleItem();
        (newer.Id.Id, newer.Level).ShouldBe((2031, LogLevel.Warning));
        newer.Message.ShouldContain($"'{Key}'");
        newer.Message.ShouldContain("4");

        // A throwing step, a chain gap and a step returning null keep the state, with a Warning naming key and type.
        var throwing = new Dictionary<int, Func<JsonNode, JsonNode>> { [1] = _ => throw new InvalidOperationException("boom") };
        h.Reader.TryRead(EnvelopeWriter.Write("{}", 1, _now), Key, typeof(Tally), 2, throwing, null, _now, out state).ShouldBeFalse();
        h.Reader.TryRead(EnvelopeWriter.Write("{}", 1, _now), Key, typeof(Tally), 2, _none, null, _now, out state).ShouldBeFalse();
        var nulling = new Dictionary<int, Func<JsonNode, JsonNode>> { [1] = _ => null! };
        h.Reader.TryRead(EnvelopeWriter.Write("{}", 1, _now), Key, typeof(Tally), 2, nulling, null, _now, out state).ShouldBeFalse();
        state.ShouldBeNull();
        var failures = h.Logs.GetSnapshot(clear: true);
        failures.Select(r => (r.Id.Id, r.Level)).ShouldBe([(2033, LogLevel.Warning), (2033, LogLevel.Warning), (2033, LogLevel.Warning)]);
        failures[0].Exception.ShouldBeOfType<InvalidOperationException>();
        failures[1].Exception.ShouldBeOfType<KeyNotFoundException>();
        failures[2].Exception.ShouldBeNull();
        failures.ShouldAllBe(r => r.Message.Contains($"'{Key}'") && r.Message.Contains(typeof(Tally).FullName!));

        // Malformed text: Warning, never throws.
        h.Reader.TryRead("""{"v":1,"at":""", Key, typeof(Tally), 1, _none, null, _now, out state).ShouldBeFalse();
        var malformed = h.Logs.GetSnapshot(clear: true).ShouldHaveSingleItem();
        (malformed.Id.Id, malformed.Level).ShouldBe((2033, LogLevel.Warning));
        malformed.Exception.ShouldBeAssignableTo<JsonException>();

        // A lone surrogate is not JSON text either (ArgumentException, not JsonException): Warning, never throws.
        h.Reader.TryRead("\"\ud800\"", Key, typeof(Tally), 1, _none, null, _now, out state).ShouldBeFalse();
        var surrogate = h.Logs.GetSnapshot(clear: true).ShouldHaveSingleItem();
        (surrogate.Id.Id, surrogate.Level).ShouldBe((2033, LogLevel.Warning));
        surrogate.Exception.ShouldBeAssignableTo<ArgumentException>();

        // A payload that fails to deserialize (wrong shape, a throwing constructor, JSON null) keeps the state: the
        // store's gateway logs the Warning naming key and type (EventId 1071), or the reader does for null.
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":"many"}""", 1, _now), Key, typeof(Tally), 1, _none, null, _now, out state).ShouldBeFalse();
        h.Reader.TryRead(EnvelopeWriter.Write("""{"value":-1}""", 1, _now), Key, typeof(Picky), 1, _none, null, _now, out state).ShouldBeFalse();
        h.Reader.TryRead(EnvelopeWriter.Write("null", 1, _now), Key, typeof(Tally), 1, _none, null, _now, out state).ShouldBeFalse();
        state.ShouldBeNull();
        var undeserializable = h.Logs.GetSnapshot(clear: true);
        undeserializable.Select(r => (r.Id.Id, r.Level)).ShouldBe([(1071, LogLevel.Warning), (1071, LogLevel.Warning), (2033, LogLevel.Warning)]);
        undeserializable[1].Exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        undeserializable[1].Message.ShouldContain($"'{Key}'");
        undeserializable[1].Message.ShouldContain(typeof(Picky).FullName!);
        undeserializable[2].Message.ShouldContain(typeof(Tally).FullName!);
    }

    [Fact]
    public void Persist_EnvelopeRoundTrip_Property()
    {
        using var h = Build();

        // Order-sensitive steps (x -> 3x + from), so a reader running them out of order fails.
        var steps = new Dictionary<int, Func<JsonNode, JsonNode>>();
        for (var from = 0; from < 40; from++)
        {
            var add = from;
            steps[from] = node =>
            {
                node["value"] = (node["value"]!.GetValue<long>() * 3) + add;
                return node;
            };
        }

        var maxAge = TimeSpan.FromHours(1);
        var gen = Gen.Select(Gen.Long[-1_000_000, 1_000_000], Gen.Int[1, 20], Gen.Int[0, 20], Gen.Long[0, 2 * maxAge.Ticks]);
        Property.Check(gen, sample =>
        {
            var (value, written, extra, ageTicks) = sample;
            var envelope = EnvelopeWriter.Write(h.Payload(new Tally(value), typeof(Tally)), written, _now - TimeSpan.FromTicks(ageTicks));

            // Written, read and migrated: the state folded through written..version-1 in order, unless older than MaxAge.
            var version = written + extra;
            var expected = Enumerable.Range(written, extra).Aggregate(value, (acc, from) => (acc * 3) + from);
            var found = h.Reader.TryRead(envelope, Key, typeof(Tally), version, steps, maxAge, _now, out var state);
            found.ShouldBe(ageTicks <= maxAge.Ticks);
            state.ShouldBe(found ? new Tally(expected) : null);

            // A newer version reads as not found.
            h.Reader.TryRead(envelope, Key, typeof(Tally), written - 1, steps, null, _now, out state).ShouldBeFalse();
            state.ShouldBeNull();
        });

        h.TakeLogs();

        // A second shape: strings needing escapes and an enum converter, written and read back equal.
        var statuses = Gen.Select(Gen.Enum<Mood>(), Gen.String[Gen.Char["aZ\u00e9\"\\ \u2028<>&\n"], 0, 30], Gen.Int[1, 20]);
        Property.Check(statuses, sample =>
        {
            var (mood, name, version) = sample;
            var envelope = EnvelopeWriter.Write(h.Payload(new Status(mood, name), typeof(Status)), version, _now);
            h.Reader.TryRead(envelope, "ducky:status", typeof(Status), version, _none, null, _now, out var state).ShouldBeTrue();
            state.ShouldBe(new Status(mood, name));
        });
        h.TakeLogs().ShouldBeEmpty();
    }

    [Fact]
    public void Persist_DeepState_RoundTripsUpToTheContextMaxDepth()
    {
        // The envelope adds one level above s: the deepest state the serializer writes still reads back, plain and
        // migrated, at the default MaxDepth and at a raised one.
        var identity = new Dictionary<int, Func<JsonNode, JsonNode>> { [1] = node => node };
        foreach (var (context, levels) in new (JsonSerializerContext, int)[] { (EnvelopeJson.Default, 63), (DeepEnvelopeJson.Default, 127) })
        {
            using var h = Build(context);
            var deep = Nest.Deep(levels);
            var envelope = EnvelopeWriter.Write(h.Payload(deep, typeof(Nest)), 1, _now);

            h.Reader.TryRead(envelope, "ducky:nest", typeof(Nest), 1, _none, null, _now, out var state).ShouldBeTrue();
            state.ShouldBe(deep);
            h.Reader.TryRead(envelope, "ducky:nest", typeof(Nest), 2, identity, null, _now, out state).ShouldBeTrue();
            state.ShouldBe(deep);
            h.TakeLogs().ShouldBeEmpty();
        }
    }

    [Fact]
    public void Persist_OneXFormat_Discarded()
    {
        using var h = Build();
        string[] oneX =
        [
            // 1.x RootStateSerializer: {Type: AssemblyQualifiedName, Value}.
            """{"Type":"App.Tally, App, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null","Value":{"Value":3}}""",

            // 1.x TypedLocalStoragePersistenceProvider: a slice dictionary carrying type names.
            """{"slices":{"tally":{"typeName":"App.Tally, App, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null","stateJson":{"value":3}}}}""",

            // A bare 1.x state, and values that are not objects.
            """{"value":3}""",
            "[1,2]",
            "\"tally\"",
            "3",

            // Envelope look-alikes missing or mistyping one member.
            """{"at":"2026-09-30T12:00:00Z","s":{"value":3}}""",
            """{"v":"1","at":"2026-09-30T12:00:00Z","s":{"value":3}}""",
            """{"v":1.5,"at":"2026-09-30T12:00:00Z","s":{"value":3}}""",
            """{"v":1,"s":{"value":3}}""",
            """{"v":1,"at":"yesterday","s":{"value":3}}""",
            """{"v":1,"at":42,"s":{"value":3}}""",
            """{"v":1,"at":"2026-09-30T12:00:00Z"}""",
        ];

        foreach (var text in oneX)
        {
            h.Reader.TryRead(text, Key, typeof(Tally), 1, _none, null, _now, out var state).ShouldBeFalse(text);
            state.ShouldBeNull();
            var record = h.Logs.GetSnapshot(clear: true).ShouldHaveSingleItem(text);
            (record.Id.Id, record.Level).ShouldBe((2030, LogLevel.Debug), text);
            record.Message.ShouldContain($"'{Key}'");
        }
    }

    [Fact]
    public void Persist_PolymorphicState_RoundTripsViaDeclaredType()
    {
        using var h = Build();
        var slice = h.Store.Slices.OfType<ShapeSlice>().Single();

        // Written and read with the declared abstract type, so the discriminator travels in s and no type name does.
        var envelope = EnvelopeWriter.Write(h.Payload(new Circle(2.5), slice.StateType), 1, _now);
        envelope.ShouldBe("""{"v":1,"at":"2026-09-30T12:00:00Z","s":{"$type":"circle","radius":2.5}}""");
        envelope.ShouldNotContain("Ducky.Blazor.Tests");

        h.Reader.TryRead(envelope, "ducky:shape", slice.StateType, 1, _none, null, _now, out var state).ShouldBeTrue();
        state.ShouldBe(new Circle(2.5));
        h.TakeLogs().ShouldBeEmpty();
    }

    [Fact]
    public void Persist_ContextWithCamelCaseAndEnumStringConverter_RoundTripsUsingContextOptions()
    {
        using var h = Build();

        // UseJson(context) brings the context's camelCase naming and its JsonStringEnumConverter<Mood> to s.
        var envelope = EnvelopeWriter.Write(h.Payload(new Status(Mood.Grumpy, "Duck"), typeof(Status)), 1, _now);
        envelope.ShouldBe("""{"v":1,"at":"2026-09-30T12:00:00Z","s":{"currentMood":"Grumpy","displayName":"Duck"}}""");

        h.Reader.TryRead(envelope, "ducky:status", typeof(Status), 1, _none, null, _now, out var state).ShouldBeTrue();
        state.ShouldBe(new Status(Mood.Grumpy, "Duck"));
        h.TakeLogs().ShouldBeEmpty();
    }
}
