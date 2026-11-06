using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bustand.Core;

namespace Bustand.Components;

/// <summary>
/// Provides a scoped store instance to child components via CascadingValue.
/// Use this to create isolated store instances for subtrees of components.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
/// <typeparam name="TState">The state type.</typeparam>
/// <remarks>
/// <para>
/// ZustandScope creates a new store instance (via DI) and cascades it to all child components.
/// Children can access the store via [CascadingParameter].
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// &lt;ZustandScope TStore="CounterStore" TState="CounterState"&gt;
///     &lt;Counter /&gt;  &lt;!-- Gets scoped CounterStore --&gt;
/// &lt;/ZustandScope&gt;
/// </code>
/// </para>
/// </remarks>
public partial class ZustandScope<TStore, TState> : IDisposable
    where TStore : class, IStore
    where TState : class
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>
    /// Child content that receives the cascaded store.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Optional: Provide an existing store instance instead of creating via DI.
    /// </summary>
    [Parameter]
    public TStore? Instance { get; set; }

    private TStore? _store;
    private IServiceScope? _scope;

    /// <summary>
    /// The store instance being cascaded.
    /// </summary>
    protected TStore Store => _store ?? throw new InvalidOperationException("Store not initialized");

    protected override void OnInitialized()
    {
        if (Instance != null)
        {
            _store = Instance;
        }
        else
        {
            // Create a new scope to get a new store instance
            _scope = ServiceProvider.CreateScope();
            _store = _scope.ServiceProvider.GetRequiredService<TStore>();
        }

        // Ensure async initialization
        if (_store is ZustandStore<TState> zustandStore)
        {
            _ = zustandStore.EnsureInitializedAsync();
        }
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
