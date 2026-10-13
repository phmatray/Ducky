using System.Text.Json.Serialization;

namespace Ducky.Blazor.Generated.Tests;

// The [Persist]/[Prerender] slices AddDuckyGenerated registers for every test of this project, so each test composes
// with all of them: CartSlice's Version 2 needs the builder's Migrate(1, ...) (DUCKY311), which every chain adds.
internal sealed record Notes(int Count);

internal sealed record Cart(int Count);

internal sealed record Todos(int Count);

// A bare [Persist]: GEN-02 emits PersistAttributeDefaults<NotesSlice>(b, o => { }).
[Persist]
internal sealed class NotesSlice : Slice<Notes>
{
    public override string Key => "notes";

    protected override Notes Initial => new(0);
}

// GEN-02 emits PersistAttributeDefaults<CartSlice>(b, o => { o.Version = 2; o.SyncAcrossTabs = true; }).
[Persist(Version = 2, SyncAcrossTabs = true)]
internal sealed class CartSlice : Slice<Cart>
{
    public override string Key => "cart";

    protected override Cart Initial => new(0);
}

// GEN-02 emits Prerender<TodosSlice>(b).
[Prerender]
internal sealed class TodosSlice : Slice<Todos>
{
    public override string Key => "todos";

    protected override Todos Initial => new(0);
}

[JsonSerializable(typeof(Notes))]
[JsonSerializable(typeof(Cart))]
[JsonSerializable(typeof(Todos))]
internal sealed partial class GeneratedJson : JsonSerializerContext;

// Every state but Notes: the generated [Persist] call requires its JSON type info like Persist<T> (DUCKY306).
[JsonSerializable(typeof(Cart))]
[JsonSerializable(typeof(Todos))]
internal sealed partial class WithoutNotesJson : JsonSerializerContext;
