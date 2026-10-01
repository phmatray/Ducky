using System.Reflection;
using System.Reflection.Emit;
using Ducky.Tests.SliceKeyFixtures;

namespace Ducky.Tests;

// SPEC §6.1.
public sealed class SliceKeyTests
{
    [Theory]
    [InlineData(typeof(TodoSlice), "todo")]
    [InlineData(typeof(ShoppingCartReducers), "shopping-cart")]
    [InlineData(typeof(UIStateSlice), "ui-state")]
    [InlineData(typeof(CartStore), "cart")]
    [InlineData(typeof(Features.TodosSlice), "features-todos")]
    [InlineData(typeof(CounterReducer), "counter")]
    [InlineData(typeof(TodoSliceStore), "todo-slice")]
    [InlineData(typeof(CounterReducerStore), "counter-reducer")]
    [InlineData(typeof(Cartslice), "cartslice")]
    [InlineData(typeof(Reducers), "reducers")]
    [InlineData(typeof(Cart.Slice), "cart")]
    [InlineData(typeof(Outer.Middle.InnerSlice), "outer-middle-inner")]
    [InlineData(typeof(IO.HTTPSlice), "io-http")]
    [InlineData(typeof(HTTPClientSlice), "http-client")]
    [InlineData(typeof(TodoAPISlice), "todo-api")]
    [InlineData(typeof(Todo2ApiSlice), "todo2-api")]
    public void SliceKey_FromType_Table(Type type, string expected) =>
        SliceKey.FromType(type).ShouldBe(expected);

    // Which assembly the slice lives in, for the @ducky/ reservation.
    public enum Signer
    {
        Ducky, // Ducky.dll itself
        SameKey, // another assembly signed with ducky.snk (Ducky.Tests, SPEC §18), as Ducky.Blazor is
        OtherKey, // System.Private.CoreLib
        NoKey, // a typical user assembly: empty public key token
    }

    // Non-normative (PLAN M1-01): the DUCKY303 regex and the @ducky/ reservation.
    [Theory]
    [InlineData("todo", Signer.NoKey, true)]
    [InlineData("shopping-cart", Signer.OtherKey, true)]
    [InlineData("a1-2b", Signer.OtherKey, true)]
    [InlineData("@ducky/persistence", Signer.Ducky, true)]
    [InlineData("@ducky/persistence", Signer.SameKey, true)]
    [InlineData("@ducky/cross-tab", Signer.SameKey, true)]
    [InlineData("@ducky/persistence", Signer.OtherKey, false)]
    [InlineData("@ducky/persistence", Signer.NoKey, false)]
    [InlineData("todo", Signer.Ducky, true)]
    [InlineData("", Signer.Ducky, false)]
    [InlineData("Todo", Signer.Ducky, false)]
    [InlineData("todo-", Signer.Ducky, false)]
    [InlineData("-todo", Signer.Ducky, false)]
    [InlineData("todo--list", Signer.Ducky, false)]
    [InlineData("todo_list", Signer.Ducky, false)]
    [InlineData("todo list", Signer.Ducky, false)]
    [InlineData("todo\n", Signer.Ducky, false)]
    [InlineData("@ducky/", Signer.Ducky, false)]
    [InlineData("@Ducky/todo", Signer.Ducky, false)]
    [InlineData("@other/todo", Signer.Ducky, false)]
    [InlineData("ducky/todo", Signer.Ducky, false)]
    [InlineData("@ducky/@ducky/todo", Signer.Ducky, false)]
    public void SliceKey_Validation_Table(string key, Signer signer, bool expected) =>
        SliceKey.IsValid(key, AssemblyOf(signer)).ShouldBe(expected);

    private static Assembly AssemblyOf(Signer signer) => signer switch
    {
        Signer.Ducky => typeof(SliceKey).Assembly,
        Signer.SameKey => typeof(SliceKeyTests).Assembly,
        Signer.OtherKey => typeof(object).Assembly,
        _ => AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Unsigned"), AssemblyBuilderAccess.Run),
    };
}
