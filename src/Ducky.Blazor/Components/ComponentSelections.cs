using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Blazor;

/// <summary>
/// What <see cref="DuckyComponent"/> and <see cref="DuckyLayout"/> share (SPEC §11.2, INV-20): the registration guard and
/// one lazily created <see cref="SubscriptionCore"/>. Renderer-only.
/// </summary>
internal sealed class ComponentSelections
{
    private SubscriptionCore? _core;
    private bool _rendered;

    public Selection<T> Select<T>(
        object component,
        IStore store,
        IServiceProvider services,
        Func<Func<Task>, Task> invokeAsync,
        Action stateHasChanged,
        Func<StateSnapshot, T> selector,
        IEqualityComparer<T>? comparer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_rendered)
        {
            throw new InvalidOperationException(BlazorErrors.Format(BlazorErrors.SelectAfterFirstRender(component.GetType())));
        }

        _core ??= new(store, invokeAsync, stateHasChanged, (ILogger?)services.GetService<ILogger<SubscriptionCore>>() ?? NullLogger.Instance);
        return _core.Select(selector, comparer);
    }

    /// <summary>Runs in the after-render hook: closes registration; returns whether this is the first render.</summary>
    public bool CloseRegistration()
    {
        var first = !_rendered;
        _rendered = true;
        return first;
    }

    public void Dispose() => _core?.Dispose();
}
