namespace Ducky.Pipeline;

/// <summary>
/// Strongly-typed action context implementation.
/// </summary>
/// <typeparam name="TAction">The action type.</typeparam>
public sealed class ActionContext<TAction>(TAction action) : IActionContext
{
    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    public TAction Action { get; set; } = action;

    /// <inheritdoc />
    public Type ActionType => typeof(TAction);

    /// <inheritdoc />
    public Dictionary<string, object?> Metadata { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the pipeline has been aborted.
    /// </summary>
    public bool IsAborted { get; private set; }

    /// <inheritdoc />
    public void AbortPipeline()
    {
        IsAborted = true;
    }

    /// <inheritdoc />
    public void SetMetadata<T>(string key, T? value)
    {
        Metadata[key] = value;
    }

    /// <inheritdoc />
    public bool TryGetMetadata<T>(string key, out T? value)
    {
        if (Metadata.TryGetValue(key, out object? obj))
        {
            value = (obj is T t) ? t : default;
            // Will return true even if value is null (if the key was present)
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public T? GetOrAddMetadata<T>(string key, Func<T?> valueFactory)
    {
        if (TryGetMetadata(key, out T? value))
        {
            return value;
        }

        // If not found, create and set the value
        value = valueFactory();
        SetMetadata(key, value);
        return value;
    }
}
