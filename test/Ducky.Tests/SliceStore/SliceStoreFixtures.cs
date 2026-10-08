// SliceStores, states and effects for SliceStoreTests (SPEC §5.4, §12).
using System.Collections.Immutable;
using Ducky.Tests.EffectFixtures;

namespace Ducky.Tests.SliceStoreFixtures;

internal sealed record CartState(ImmutableList<string> Items);

internal sealed record LoggedOut;

internal sealed record Buy(string Item);

// Key "cart"; still a slice, so it reacts to actions too (§12).
internal sealed class CartStore : SliceStore<CartState>
{
    public CartStore() => On<LoggedOut>(_ => Initial);

    public int Count => State.Items.Count;

    protected override CartState Initial => new([]);

    public void Add(string item) => Set(s => s with { Items = s.Items.Add(item) });

    public Task<DispatchResult> AddAsync(string item) => SetAsync(s => s with { Items = s.Items.Add(item) });

    public Task<DispatchResult> TouchAsync() => SetAsync(s => s);

    public void Touch() => Set(s => s);

    public void SetNull() => Set(null!);

    public Task<DispatchResult> SetNullAsync() => SetAsync(null!);
}

// An effect whose constructor injects the store-owned SliceStore and calls its methods (§6.6, §12).
internal sealed class BuyEffect(CartStore cart, EffectJournal journal) : Effect<Buy>
{
    public CartStore Cart => cart;

    public override async Task Handle(Buy action, EffectContext context, CancellationToken cancellationToken)
    {
        journal.Handled.Add((this, action));
        cart.Add(action.Item);
        await cart.AddAsync(action.Item + "!").ConfigureAwait(false);
    }
}
