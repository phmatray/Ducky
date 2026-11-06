using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Bustand.Configuration;

/// <summary>
/// Configuration options for Bustand store registration.
/// </summary>
public class BustandOptions
{
    /// <summary>
    /// Assemblies to scan for stores marked with [BustandStore].
    /// If empty, scans the calling assembly.
    /// </summary>
    public List<Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Override the mode-aware default lifetime for all stores.
    /// Individual stores can still override via [BustandStore(Lifetime)].
    /// </summary>
    public ServiceLifetime? DefaultLifetimeOverride { get; set; }

    /// <summary>
    /// If true, logs a warning when Singleton stores are detected in Server mode.
    /// Default: true.
    /// </summary>
    public bool WarnOnSingletonInServerMode { get; set; } = true;

    /// <summary>
    /// Middleware types to apply to all stores, in registration order.
    /// </summary>
    internal List<Type> MiddlewareTypes { get; } = new();

    /// <summary>
    /// Gets whether DevTools middleware should be enabled.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the DevTools package will register its middleware
    /// to capture state changes for debugging and time-travel.
    /// This flag is set by calling <see cref="UseDevTools"/>.
    /// </remarks>
    internal bool DevToolsEnabled { get; private set; }

    /// <summary>
    /// Prefix for all storage keys.
    /// Default: "Bustand"
    /// Example: With prefix "MyApp", key becomes "MyApp.CounterStore"
    /// </summary>
    /// <remarks>
    /// Use a unique prefix per application to prevent key collisions when
    /// multiple apps share the same origin.
    /// </remarks>
    public string StorageKeyPrefix { get; set; } = "Bustand";

    /// <summary>
    /// JSON serialization options for state persistence.
    /// Default: camelCase naming, ignore null values.
    /// </summary>
    /// <remarks>
    /// Customize to match your state serialization needs. Ensure options
    /// are consistent between persist and restore operations.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Debounce delay for persistence writes in milliseconds.
    /// Default: 300ms
    /// </summary>
    /// <remarks>
    /// <para>
    /// State changes within this window are batched into a single write.
    /// This reduces storage I/O for rapid state updates.
    /// </para>
    /// <para>
    /// Lower values: More responsive persistence, more I/O.
    /// Higher values: Less I/O, risk of losing more recent changes on crash.
    /// </para>
    /// </remarks>
    public int PersistenceDebounceMs { get; set; } = 300;

    /// <summary>
    /// Adds the assembly containing the specified type to the scan list.
    /// </summary>
    /// <typeparam name="T">A type from the assembly to scan.</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public BustandOptions ScanAssemblyContaining<T>()
    {
        AssembliesToScan.Add(typeof(T).Assembly);
        return this;
    }

    /// <summary>
    /// Adds the specified assembly to the scan list.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This options instance for chaining.</returns>
    public BustandOptions ScanAssembly(Assembly assembly)
    {
        AssembliesToScan.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers a middleware to be applied to all stores.
    /// Middleware execute in registration order.
    /// </summary>
    /// <typeparam name="TMiddleware">
    /// Middleware type implementing <c>IMiddleware&lt;TState&gt;</c>.
    /// Must be an open generic type (e.g., <c>LoggingMiddleware&lt;&gt;</c>).
    /// </typeparam>
    /// <returns>This options instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Middleware types are registered as open generics and closed over each store's state type
    /// when the store is constructed. This allows a single middleware registration to work with
    /// all stores regardless of their state type.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddBustand(options =>
    /// {
    ///     options.UseMiddleware&lt;LoggingMiddleware&lt;&gt;&gt;();
    ///     options.UseMiddleware&lt;ValidationMiddleware&lt;&gt;&gt;();
    /// });
    /// </code>
    /// </example>
    public BustandOptions UseMiddleware<TMiddleware>() where TMiddleware : class
    {
        MiddlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>
    /// Enables DevTools middleware for state change capture.
    /// Call this only when DevTools services are registered (development environment).
    /// </summary>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// DevTools middleware captures all state changes for inspection in the DevTools page.
    /// This should only be enabled in development environments for security reasons.
    /// </para>
    /// <para>
    /// <b>Important:</b> This method sets a flag that the DevTools package reads.
    /// You must also call <c>AddBustandDevTools()</c> to register the DevTools services.
    /// The DevTools package will automatically register its middleware when both
    /// <c>UseDevTools()</c> is called and DevTools services are registered.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// builder.Services.AddBustand(options => options
    ///     .ScanAssemblyContaining&lt;CounterStore&gt;()
    ///     .UseLogging()
    ///     .UseDevTools());
    ///
    /// // Also register DevTools services in development:
    /// if (builder.Environment.IsDevelopment())
    /// {
    ///     builder.Services.AddBustandDevTools(builder.Environment);
    /// }
    /// </code>
    /// </example>
    public BustandOptions UseDevTools()
    {
        DevToolsEnabled = true;
        return this;
    }

    /// <summary>
    /// Builds the full storage key for a store type.
    /// </summary>
    /// <param name="storeType">The store type.</param>
    /// <param name="customKey">Optional custom key from PersistAttribute.</param>
    /// <returns>The full storage key with prefix.</returns>
    internal string BuildStorageKey(Type storeType, string? customKey)
    {
        var key = customKey ?? storeType.FullName ?? storeType.Name;
        return string.IsNullOrEmpty(StorageKeyPrefix)
            ? key
            : $"{StorageKeyPrefix}.{key}";
    }
}
