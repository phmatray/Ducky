namespace Ducky.Pipeline;

/// <summary>
/// Represents the context for an action being processed by the middleware pipeline.
/// </summary>
public interface IActionContext
{
    /// <summary>
    /// Gets the type of the action.
    /// </summary>
    Type ActionType { get; }

    /// <summary>
    /// Gets the metadata dictionary for the action context.
    /// </summary>
    Dictionary<string, object?> Metadata { get; }

    /// <summary>
    /// Aborts pipeline processing for this action.
    /// </summary>
    void AbortPipeline();

    /// <summary>
    /// Sets a metadata value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    void SetMetadata<T>(string key, T? value);

    /// <summary>
    /// Tries to get a metadata value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The out metadata value if found, otherwise default.</param>
    /// <returns>True if the metadata key exists; otherwise, false.</returns>
    bool TryGetMetadata<T>(string key, out T? value);

    /// <summary>
    /// Gets a metadata value, or adds it if not present.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="valueFactory">A factory function to create the value if not found.</param>
    /// <returns>The existing or new metadata value.</returns>
    T? GetOrAddMetadata<T>(string key, Func<T?> valueFactory);
}
