using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor.Tests.Interactivity;

// What the store-scope party saw: the gate it reached, its own (store-scope) services, and the order of events.
internal sealed class StoreScopeSeen
{
    public InteractivityGate? Gate { get; set; }

    public IServiceProvider? StoreServices { get; set; }

    public IServiceProvider? Registered { get; set; }

    public List<string> Order { get; } = [];
}

// A store-scope built-in stand-in (PrerenderHandoff, PersistenceMiddleware, DevToolsMiddleware): it reaches the gate
// through the PersistenceSlice registration and waits for the first component registration.
internal sealed class StoreScopeProbe : Middleware
{
    private readonly StoreScopeSeen _seen;

    public StoreScopeProbe(PersistenceSlice persistence, IServiceProvider storeServices, StoreScopeSeen seen)
    {
        (_seen, seen.Gate, seen.StoreServices) = (seen, persistence.Gate, storeServices);
        seen.Order.Add("materialized");
    }

    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        var gate = _seen.Gate!;
        gate.OnFirstRegistration(() => _seen.Registered = gate.Services);
        return default;
    }
}

// A Ducky component showing the counter; it records its renders in Order when given one.
internal sealed class CounterView : DuckyComponent
{
    private Selection<int> _count = null!;

    [Parameter]
    public List<string>? Order { get; set; }

    protected override void OnInitialized() => _count = Select((Counter c) => c.Value);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Order?.Add("render");
        builder.AddContent(0, _count.Value);
    }
}
