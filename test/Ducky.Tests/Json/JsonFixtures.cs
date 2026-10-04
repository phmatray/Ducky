// Types and a source-generated context for DuckyJsonTests (SPEC §10).
using System.Text.Json.Serialization;

namespace Ducky.Tests.JsonFixtures;

internal sealed record Note(string Text);

internal sealed class NoteSlice : Slice<Note>
{
    protected override Note Initial => new("initial");
}

// Its constructor rejects a negative count, as Ducky's EntityState rejects duplicate ids (§10): STJ lets that throw
// through unwrapped.
internal sealed class Picky
{
    [JsonConstructor]
    public Picky(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Count = count;
    }

    public int Count { get; }
}

internal sealed class PickySlice : Slice<Picky>
{
    protected override Picky Initial => new(0);
}

// NaN or Infinity throws under the default number handling: a value-dependent failure.
internal sealed record Measure(double Value);

// Not in TestJson.
internal sealed record Uncovered(int Value);

internal enum Mood
{
    Calm,
    Grumpy,
}

internal sealed record Status(Mood Mood);

// Two derived types share a discriminator: STJ throws InvalidOperationException from every type info lookup of Shape,
// read-only options caching the exception.
[JsonPolymorphic]
[JsonDerivedType(typeof(Circle), "shape")]
[JsonDerivedType(typeof(Square), "shape")]
internal abstract record Shape;

internal sealed record Circle : Shape;

internal sealed record Square : Shape;

internal sealed class ShapeSlice : Slice<Shape>
{
    protected override Shape Initial => new Circle();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(JsonStringEnumConverter<Mood>)])]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(Picky))]
[JsonSerializable(typeof(Measure))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(Shape))]
internal sealed partial class TestJson : JsonSerializerContext;

// The same types with default options.
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(Status))]
internal sealed partial class PlainJson : JsonSerializerContext;
