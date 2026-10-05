using System.Text.Json.Serialization;

namespace Ducky.Blazor.Tests.Persistence;

internal sealed record Tally(long Value);

internal sealed class TallySlice : Slice<Tally>
{
    protected override Tally Initial => new(0);
}

[JsonPolymorphic]
[JsonDerivedType(typeof(Circle), "circle")]
[JsonDerivedType(typeof(Square), "square")]
internal abstract record Shape;

internal sealed record Circle(double Radius) : Shape;

internal sealed record Square(double Side) : Shape;

internal sealed class ShapeSlice : Slice<Shape>
{
    protected override Shape Initial => new Square(1);
}

internal enum Mood
{
    Calm,
    Grumpy,
}

internal sealed record Status(Mood CurrentMood, string DisplayName);

internal sealed class StatusSlice : Slice<Status>
{
    protected override Status Initial => new(Mood.Calm, "");
}

// A state whose constructor throws during deserialization, as EntityState's duplicate-id check does.
internal sealed record Picky
{
    public Picky(int value) => Value = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));

    public int Value { get; }
}

// A recursive state, as deep as its levels: Inner is null at the bottom.
internal sealed record Nest(int Level, Nest? Inner)
{
    public static Nest Deep(int levels) => new(levels, levels == 1 ? null : Deep(levels - 1));
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(JsonStringEnumConverter<Mood>)])]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Shape))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(Picky))]
[JsonSerializable(typeof(Nest))]
internal sealed partial class EnvelopeJson : JsonSerializerContext;

// The same context with MaxDepth raised, to store a deep tree.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(JsonStringEnumConverter<Mood>)], MaxDepth = 128)]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Shape))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(Nest))]
internal sealed partial class DeepEnvelopeJson : JsonSerializerContext;
