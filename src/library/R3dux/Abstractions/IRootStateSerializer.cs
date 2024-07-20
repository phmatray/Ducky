namespace R3dux;

/// <summary>
/// Provides methods for serializing and deserializing <see cref="RootState"/> instances.
/// </summary>
public interface IRootStateSerializer
{
    /// <summary>
    /// Serializes the specified <see cref="RootState"/> to a JSON string.
    /// </summary>
    /// <param name="rootState">The <see cref="RootState"/> to serialize.</param>
    /// <returns>The JSON string representation of the state.</returns>
    string Serialize(RootState rootState);
    
    /// <summary>
    /// Serializes the slice state associated with the specified key to a JSON string.
    /// </summary
    /// <param name="rootState">The <see cref="RootState"/> to serialize.</param>
    /// <param name="key">The key of the slice state to serialize.</param>
    /// <returns>The JSON string representation of the slice state.</returns>
    string Serialize(RootState rootState, string key);

    /// <summary>
    /// Deserializes a <see cref="RootState"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representation of the state.</param>
    /// <returns>A new instance of <see cref="RootState"/> with the deserialized state.</returns>
    RootState Deserialize(string json);

    /// <summary>
    /// Saves the specified <see cref="RootState"/> to a file.
    /// </summary>
    /// <param name="rootState">The <see cref="RootState"/> to save.</param>
    /// <param name="filePath">The path of the file to save the state to.</param>
    void SaveToFile(RootState rootState, string filePath);

    /// <summary>
    /// Loads a <see cref="RootState"/> from a file.
    /// </summary>
    /// <param name="filePath">The path of the file to load the state from.</param>
    /// <returns>A new instance of <see cref="RootState"/> with the loaded state.</returns>
    RootState LoadFromFile(string filePath);
}