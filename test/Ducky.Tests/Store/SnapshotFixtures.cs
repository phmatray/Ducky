// Slices and states for StateSnapshotTests (SPEC §6.2).
#pragma warning disable CA1812 // justification: some fixtures are only used as type arguments
namespace Ducky.Tests.SnapshotFixtures;

internal sealed record Todos(int Count);

internal sealed record Unregistered;

internal sealed class TodoSlice : Slice<Todos>
{
    protected override Todos Initial => new(0);
}

internal sealed record Box<T>;

// Box<Box<...>> nests give as many distinct state types as a test needs.
internal sealed class BoxSlice<T>(int index) : Slice<Box<T>>
{
    public override string Key => $"box{index}";

    protected override Box<T> Initial => new();
}
