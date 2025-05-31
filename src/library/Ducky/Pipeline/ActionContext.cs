namespace Ducky.Pipeline;

/// <summary>
/// Carries an action (and metadata) through the middleware pipeline.
/// </summary>
public sealed class ActionContext
{
    /// <summary>
    /// The original action object.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Arbitrary metadata bag for handlers.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; } = [];

    /// <summary>
    /// Returns true if a middleware aborted further processing.
    /// </summary>
    public bool IsAborted { get; private set; }

    /// <summary>
    /// The current root state of the store.
    /// </summary>
    public IRootState RootState { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionContext"/> class with the specified action.
    /// </summary>
    /// <param name="action">The action to carry through the pipeline.</param>
    public ActionContext(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
    }

    /// <summary>
    /// Call to stop the pipeline after this middleware.
    /// </summary>
    public void Abort()
    {
        IsAborted = true;
    }

    /// <summary>
    /// Sets a metadata value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void SetMetadata<T>(string key, T? value)
    {
        Metadata[key] = value;
    }

    /// <summary>
    /// Tries to get a metadata value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The out metadata value if found, otherwise default.</param>
    /// <returns>True if the metadata key exists; otherwise, false.</returns>
    public bool TryGetMetadata<T>(string key, out T? value)
    {
        if (Metadata.TryGetValue(key, out object? obj))
        {
            value = obj is T t ? t : default;
            // Will return true even if value is null (if the key was present)
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a metadata value, or adds it if not present.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="valueFactory">A factory function to create the value if not found.</param>
    /// <returns>The existing or new metadata value.</returns>
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
