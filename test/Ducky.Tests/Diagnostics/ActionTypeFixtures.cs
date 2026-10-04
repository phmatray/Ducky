// Actions for ActionTypesTests (SPEC §9, §10).
using System.Text.Json.Serialization;

namespace Ducky.Tests.ActionTypeFixtures
{
    internal sealed record Plain;

    [ActionType("todos/added")]
    internal sealed record Added(string Text);

    internal sealed record Item;

    internal sealed record Loaded<T>(T Value);

    internal sealed record Pair<TFirst, TSecond>(TFirst First, TSecond Second);

    [ActionType("cache/hit")]
    internal sealed record Hit<T>(T Value);

    internal static class Outer
    {
        internal sealed record Inner;
    }

    // Each level of a nested generic type carries its own arguments: GenericOuter<T>.Inner, GenericOuter<T>.Nested<U>.
    internal static class GenericOuter<T>
    {
        internal sealed record Inner;

        internal sealed record Nested<TInner>;
    }

    [ActionType("base/named")]
    internal class Named;

    // [ActionType] is read with inherit: false, so a derived action has its own name.
    internal sealed class DerivedFromNamed : Named;

    // No type info in ProbeJson.
    [ActionType("probe/missing")]
    internal sealed record MissingProbe(int Value);

    // STJ refuses to serialize a System.Type (NotSupportedException).
    [ActionType("probe/type")]
    internal sealed record TypeProbe(Type Type);

    // A self-reference is a cycle (JsonException); an acyclic instance serializes.
    [ActionType("probe/cyclic")]
    internal sealed class CyclicProbe
    {
        public CyclicProbe? Next { get; set; }
    }

    // Metadata mode: the serializer's cycle check throws JsonException (the fast path would hit the writer's depth limit).
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(TypeProbe))]
    [JsonSerializable(typeof(CyclicProbe))]
    internal sealed partial class ProbeJson : JsonSerializerContext;
}

#pragma warning disable CA1050 // justification: the global-namespace row of ActionType_Names_Table needs a type without a namespace
internal sealed record GlobalAction;
#pragma warning restore CA1050
