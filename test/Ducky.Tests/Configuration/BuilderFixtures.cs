// Slices, states and actions for DuckyBuilderTests (SPEC §5.1, §8.2, INV-31). Each misconfigured slice breaks one rule.
#pragma warning disable CA1812 // justification: the fixtures are only used as type arguments of AddSlice
namespace Ducky.Tests.BuilderFixtures;

internal sealed record Cart(int Items);

internal sealed record Order(int Lines);

internal sealed record Coupon(string Code);

internal sealed record Discount(int Percent);

internal sealed record Box<T>(T Value);

internal sealed record AddItem;

internal sealed record Explode;

internal abstract record AnyAction;

internal sealed class Marker;

// Valid: key "cart". AddItem adds one item; Explode throws, so a test can see the store's logger.
internal sealed class CartSlice : Slice<Cart>
{
    public CartSlice()
    {
        On<AddItem>(state => state with { Items = state.Items + 1 });
        On<Explode>(_ => throw new InvalidOperationException("explode"));
    }

    protected override Cart Initial => new(0);
}

// Valid: key "order".
internal sealed class OrderSlice : Slice<Order>
{
    protected override Order Initial => new(0);
}

// DUCKY303.
internal sealed class BadKeySlice : Slice<Coupon>
{
    public override string Key => "Bad_Key";

    protected override Coupon Initial => new("");
}

// DUCKY304 next to CartSlice: same key, its own state type.
internal sealed class CartCopySlice : Slice<Discount>
{
    public override string Key => "cart";

    protected override Discount Initial => new(0);
}

// DUCKY305 next to CartSlice: its own key, the same state type.
internal sealed class CartTwinSlice : Slice<Cart>
{
    public override string Key => "cart-twin";

    protected override Cart Initial => new(0);
}

// DUCKY302: generic, without an explicit Key.
internal sealed class BoxSlice<T> : Slice<Box<T>>
{
    protected override Box<T> Initial => new(default!);
}

// Generic with an explicit Key: valid.
internal sealed class KeyedBoxSlice<T> : Slice<Box<T>>
{
    public override string Key => "keyed-box";

    protected override Box<T> Initial => new(default!);
}

// DUCKY307, thrown by the constructor.
internal sealed class NonConcreteHandlerSlice : Slice<Order>
{
    public NonConcreteHandlerSlice() => On<AnyAction>(state => state);

    protected override Order Initial => new(0);
}

// DUCKY308, thrown by the constructor.
internal sealed class DuplicateHandlerSlice : Slice<Order>
{
    public DuplicateHandlerSlice()
    {
        On<AddItem>(state => state);
        On<AddItem>(state => state);
    }

    protected override Order Initial => new(0);
}

internal sealed record Note(string Text);

// A slice constructor that throws an ordinary exception: reported at first resolution, never out of AddDucky (INV-31).
internal sealed class ThrowingCtorSlice : Slice<Note>
{
    public ThrowingCtorSlice() => throw new InvalidOperationException("ctor");

    protected override Note Initial => new("");
}

// A Key override that throws.
internal sealed class ThrowingKeySlice : Slice<Note>
{
    public override string Key => throw new FormatException("key");

    protected override Note Initial => new("");
}

// DUCKY303: a Key override that returns null.
internal sealed class NullKeySlice : Slice<Note>
{
    public override string Key => null!;

    protected override Note Initial => new("");
}
