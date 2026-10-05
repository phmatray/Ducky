namespace Ducky.Blazor;

// The library status slice (SPEC §11.5), registered by AddBlazor. Each store owns its instance, so its DI registration,
// which follows the store's lifetime, is how every party reaches the store's one InteractivityGate (§11.4), from any scope.
internal sealed class PersistenceSlice : Slice<PersistenceState>
{
    public override string Key => "@ducky/persistence";

    public InteractivityGate Gate { get; } = new();

    // Always Hydrated: a slice built with new() can't know whether anything is persisted.
    protected override PersistenceState Initial => new(PersistenceStatus.Hydrated);
}
