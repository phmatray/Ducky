// Types whose names SliceKey_FromType_Table derives keys from (SPEC §6.1); the namespace never enters a key.
#pragma warning disable CA1812, S2094 // justification: name-only fixtures, never instantiated
namespace Ducky.Tests.SliceKeyFixtures;

internal sealed class TodoSlice;

internal sealed class ShoppingCartReducers;

internal sealed class UIStateSlice;

internal sealed class CartStore;

internal sealed class CounterReducer;

internal sealed class TodoSliceStore;

internal sealed class CounterReducerStore;

internal sealed class Cartslice;

internal sealed class Reducers;

internal sealed class HTTPClientSlice;

internal sealed class TodoAPISlice;

internal sealed class Todo2ApiSlice;

internal static class Features
{
    internal sealed class TodosSlice;
}

internal static class IO
{
    internal sealed class HTTPSlice;
}

internal static class Cart
{
    internal sealed class Slice;
}

internal static class Outer
{
    internal static class Middle
    {
        internal sealed class InnerSlice;
    }
}
