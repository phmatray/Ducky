using Microsoft.Extensions.DependencyInjection;

namespace Bustand.Attributes;

/// <summary>
/// Marks a class as a Bustand store for auto-discovery.
/// Only classes with this attribute are registered by AddBustand().
/// </summary>
/// <example>
/// <code>
/// [BustandStore] // Uses mode-aware default lifetime
/// public class CounterStore : ZustandStore&lt;CounterState&gt; { }
///
/// [BustandStore(ServiceLifetime.Singleton)] // Explicit override
/// public class AppConfigStore : ZustandStore&lt;AppConfigState&gt; { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BustandStoreAttribute : Attribute
{
    /// <summary>
    /// Optional explicit lifetime. If null, uses mode-aware default:
    /// Singleton in WebAssembly, Scoped in Server.
    /// </summary>
    public ServiceLifetime? Lifetime { get; }

    /// <summary>
    /// Creates a BustandStore attribute with mode-aware default lifetime.
    /// </summary>
    public BustandStoreAttribute()
    {
        Lifetime = null;
    }

    /// <summary>
    /// Creates a BustandStore attribute with explicit lifetime override.
    /// </summary>
    /// <param name="lifetime">The service lifetime to use for this store.</param>
    public BustandStoreAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }
}
