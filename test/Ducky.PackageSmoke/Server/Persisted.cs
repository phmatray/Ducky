using System.Text.Json.Serialization;

namespace PackageSmoke;

// SPEC §17.9 step 1 (stage 12): the generator-emitted PersistAttributeDefaults<T> and Prerender<T> calls compile from
// packages under TreatWarningsAsErrors (no DUCKY022). Version 2 composes with Program's Migrate(1, ...) (DUCKY311).
internal sealed record Cart(int Count);

internal sealed record Todos(int Count);

[Persist(Version = 2, SyncAcrossTabs = true)]
internal sealed class CartSlice : Slice<Cart>
{
    public override string Key => "cart";

    protected override Cart Initial => new(0);
}

[Prerender]
internal sealed class TodosSlice : Slice<Todos>
{
    public override string Key => "todos";

    protected override Todos Initial => new(0);
}

[JsonSerializable(typeof(Increment))]
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Cart))]
[JsonSerializable(typeof(Todos))]
internal sealed partial class SmokeJson : JsonSerializerContext;
