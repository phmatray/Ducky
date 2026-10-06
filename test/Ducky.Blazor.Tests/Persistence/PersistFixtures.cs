using System.Text.Json.Serialization;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Prerender;

namespace Ducky.Blazor.Tests.Persistence;

internal sealed record Orphan(int Value);

// A slice the persistence tests register with Persist<T>/Prerender<T> but never add (DUCKY310).
internal sealed class OrphanSlice : Slice<Orphan>
{
    protected override Orphan Initial => new(0);
}

internal sealed record Failures(IReadOnlyList<ReducerFailed> Items);

// Records every ReducerFailed the store routes (strict mode's unhandled-action report included).
internal sealed class FailureSlice : Slice<Failures>
{
    public FailureSlice() => On<ReducerFailed>(static (state, action) => new([.. state.Items, action]));

    protected override Failures Initial => new([]);
}

// Every state the registration tests persist, so their only DUCKY306 is the one a test asks for.
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(Products))]
[JsonSerializable(typeof(Ratio))]
[JsonSerializable(typeof(Orphan))]
internal sealed partial class PersistJson : JsonSerializerContext;
