using System.ComponentModel;
using TheBlazorState.Storage;

namespace TheBlazorState.Configuration;

/// <summary>
/// Per-property configuration available in ConfigureState(StateContext ctx).
/// The source generator creates one of these per [Persist] or [Shared] property.
/// </summary>
public sealed class PropertyConfigurator<T>
{
    private object[]? _suffixParts;
    private string? _keyOverride;
    private Func<Task<T>>? _asyncFactory;
    private T _defaultValue = default!;
    private bool _hasDefaultValue;

    /// <summary>Override the storage strategy for this specific property.</summary>
    public IStorageStrategy? Storage { get; set; }

    public PropertyConfigurator<T> KeySuffix(params object[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        foreach (var part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
        }
        _suffixParts = parts;
        return this;
    }

    public PropertyConfigurator<T> KeyOverride(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _keyOverride = key;
        return this;
    }

    /// <summary>
    /// Set a default value for this property when no persisted value exists.
    /// The value is applied during OnInitialized before RestoreProperty is called.
    /// </summary>
    public PropertyConfigurator<T> DefaultValue(T value)
    {
        _defaultValue = value;
        _hasDefaultValue = true;
        return this;
    }

    /// <summary>
    /// Register an async factory to load this property's value.
    /// Called during OnInitializedAsync if the value was not restored (or is stale).
    /// </summary>
    public PropertyConfigurator<T> LoadFrom(Func<Task<T>> factory)
    {
        _asyncFactory = factory;
        return this;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ResolveKey(string baseKey)
    {
        if (_keyOverride is not null)
            return _keyOverride;

        if (_suffixParts is null or { Length: 0 })
            return baseKey;

        return baseKey + ":" + string.Join(":", _suffixParts);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasDefaultValue => _hasDefaultValue;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public T GetDefaultValue() => _defaultValue;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasAsyncFactory => _asyncFactory is not null;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<T> InvokeFactoryAsync() =>
        _asyncFactory is not null
            ? await _asyncFactory()
            : throw new InvalidOperationException("No async factory registered.");
}
