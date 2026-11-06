using TheBlazorState.Storage;

namespace TheBlazorState.Configuration;

/// <summary>
/// Global configuration for TheBlazorState, set in Program.cs via AddTheBlazorState().
/// </summary>
public sealed class TheBlazorStateOptions
{
    /// <summary>
    /// Default storage strategy used when no per-component or per-property strategy is set.
    /// Defaults to PrerenderHtml.
    /// </summary>
    public IStorageStrategy DefaultStorage { get; set; } = StorageStrategy.PrerenderHtml();

    private readonly Dictionary<string, IStorageStrategy> _customStrategies = new();

    /// <summary>Register a custom named storage strategy.</summary>
    public void AddStorage<TStrategy>(string name) where TStrategy : IStorageStrategy, new()
    {
        _customStrategies[name] = new TStrategy();
    }

    /// <summary>Register a custom named storage strategy instance.</summary>
    public void AddStorage(string name, IStorageStrategy strategy)
    {
        _customStrategies[name] = strategy;
    }

    internal IStorageStrategy? ResolveCustom(string name) =>
        _customStrategies.GetValueOrDefault(name);
}
