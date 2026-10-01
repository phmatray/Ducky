// Slices, states and actions for SliceTests (SPEC §5.3, §6.1).
#pragma warning disable CA1812 // justification: some fixtures are only used as type arguments
namespace Ducky.Tests.SliceFixtures;

internal sealed record Counter(int Value);

internal sealed record Increment;

internal sealed record Reset;

internal record Added(int Amount);

internal sealed record AddedTwice(int Amount) : Added(Amount);

internal abstract record AbstractAction;

internal interface IAction;

internal static class Outer
{
    internal abstract record NestedAction;
}

internal sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice()
    {
        On<Increment>(state => state with { Value = state.Value + 1 });
        On<Added>((state, action) => state with { Value = state.Value + action.Amount });
    }

    public int InitialReads { get; private set; }

    protected override Counter Initial
    {
        get
        {
            InitialReads++;
            return new Counter(0);
        }
    }

    public void Register<TAction>()
        where TAction : notnull => On<TAction>(state => state);
}

internal sealed class DuplicateHandlerSlice : Slice<Counter>
{
    public DuplicateHandlerSlice()
    {
        On<Increment>(state => state);
        On<Increment>((state, _) => state);
    }

    protected override Counter Initial => new(0);
}

internal sealed class NullReducerSlice : Slice<Counter>
{
    public NullReducerSlice(bool withAction)
    {
        if (withAction)
        {
            On<Increment>((Func<Counter, Increment, Counter>)null!);
        }
        else
        {
            On<Increment>((Func<Counter, Counter>)null!);
        }
    }

    protected override Counter Initial => new(0);
}

internal sealed class KeyedSlice : Slice<Counter>
{
    public override string Key => "custom";

    protected override Counter Initial => new(0);
}

internal abstract record Shape;

internal sealed record Circle : Shape;

internal sealed class PolymorphicSlice : Slice<Shape>
{
    protected override Shape Initial => new Circle();
}
