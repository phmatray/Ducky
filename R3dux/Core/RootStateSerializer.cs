using System.Text.Json;

namespace R3dux;

/// <summary>
/// Provides methods for serializing and deserializing <see cref="RootState"/> instances.
/// </summary>
public static class RootStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };
    
    /// <summary>
    /// Serializes the specified <see cref="RootState"/> to a JSON string.
    /// </summary>
    /// <param name="rootState">The <see cref="RootState"/> to serialize.</param>
    /// <returns>The JSON string representation of the state.</returns>
    public static string Serialize(this RootState rootState)
    {
        ArgumentNullException.ThrowIfNull(rootState);

        var stateDictionary = rootState.GetStateDictionary();
        var typedDictionary = stateDictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => new { Type = kvp.Value.GetType().AssemblyQualifiedName, kvp.Value }
        );

        return JsonSerializer.Serialize(typedDictionary, Options);
    }

    /// <summary>
    /// Deserializes a <see cref="RootState"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representation of the state.</param>
    /// <returns>A new instance of <see cref="RootState"/> with the deserialized state.</returns>
    public static RootState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var typedDictionary =
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, Options)
            ?? new Dictionary<string, Dictionary<string, object>>();

        var rootState = new RootState();

        foreach (var kvp in typedDictionary)
        {
            var typeName = 
                kvp.Value["Type"].ToString()
                ?? throw new InvalidOperationException("Type not found.");
            
            var type = 
                Type.GetType(typeName)
                ?? throw new InvalidOperationException($"Type '{typeName}' not found.");

            var valueJson = 
                kvp.Value["Value"].ToString()
                ?? throw new InvalidOperationException("Value not found.");
            
            var value = 
                JsonSerializer.Deserialize(valueJson, type, Options)
                ?? throw new InvalidOperationException("Value not deserialized.");

            rootState.AddOrUpdateSliceState(kvp.Key, value);
        }

        return rootState;
    }

    /// <summary>
    /// Saves the specified <see cref="RootState"/> to a file.
    /// </summary>
    /// <param name="rootState">The <see cref="RootState"/> to save.</param>
    /// <param name="filePath">The path of the file to save the state to.</param>
    public static void SaveToFile(this RootState rootState, string filePath)
    {
        ArgumentNullException.ThrowIfNull(rootState);
        ArgumentNullException.ThrowIfNull(filePath);
        
        File.WriteAllText(filePath, Serialize(rootState));
    }

    /// <summary>
    /// Loads a <see cref="RootState"/> from a file.
    /// </summary>
    /// <param name="filePath">The path of the file to load the state from.</param>
    /// <returns>A new instance of <see cref="RootState"/> with the loaded state.</returns>
    public static RootState LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
        }

        var json = File.ReadAllText(filePath);
        return Deserialize(json);
    }
}
