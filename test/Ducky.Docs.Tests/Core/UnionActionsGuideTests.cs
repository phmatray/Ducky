using Ducky.Testing;

namespace Ducky.Docs.Tests.Core;

#region union-actions
public sealed record AddProduct(string Sku);

public sealed record EmptyParcel;

public union ParcelAction(AddProduct, EmptyParcel);

public sealed record Parcel(int Items);

public sealed class ParcelSlice : Slice<Parcel>
{
    public ParcelSlice()
    {
        // One handler per case, never On<ParcelAction>(): dispatch unwraps the union to its case.
        On<AddProduct>(state => state with { Items = state.Items + 1 });
        On<EmptyParcel>(_ => Initial);
    }

    protected override Parcel Initial => new(0);
}
#endregion

// The snippets of docs/guides/core/slices-and-keys.md (unions).
public sealed class UnionActionsGuideTests
{
    [Fact]
    public async Task UnionAction_DispatchesAsItsCase()
    {
        await using var store = TestStore.Create(d => d.AddSlice<ParcelSlice>());

        await store.DispatchAsync(new ParcelAction(new AddProduct("sku-1")));
        await store.Settled();

        store.State.Get<Parcel>().ShouldBe(new Parcel(1));
        store.Processed[^1].Action.ShouldBeOfType<AddProduct>();
    }
}
