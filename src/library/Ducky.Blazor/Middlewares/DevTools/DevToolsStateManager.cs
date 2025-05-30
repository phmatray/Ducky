using System.Collections.Immutable;
using System.Text.Json;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Manages state restoration and serialization for DevTools integration.
/// Handles converting between JSON representations and Ducky state objects.
/// </summary>
public class DevToolsStateManager
{
    private readonly JsonSerializerOptions _jsonOptions;
    private ImmutableSortedDictionary<string, object>? _initialState;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsStateManager"/> class.
    /// </summary>
    public DevToolsStateManager()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Sets the initial state for reset operations.
    /// </summary>
    /// <param name="initialState">The initial state dictionary.</param>
    public void SetInitialState(ImmutableSortedDictionary<string, object> initialState)
    {
        _initialState = initialState;
    }

    /// <summary>
    /// Serializes a root state to JSON for DevTools.
    /// </summary>
    /// <param name="rootState">The root state to serialize.</param>
    /// <returns>JSON representation of the state.</returns>
    public string SerializeState(IRootState rootState)
    {
        try
        {
            ImmutableSortedDictionary<string, object> stateDict = rootState.GetStateDictionary();
            return JsonSerializer.Serialize(stateDict, _jsonOptions);
        }
        catch (Exception ex)
        {
            // Log error and return empty state if serialization fails
            Console.WriteLine($"DevTools state serialization failed: {ex.Message}");
            return "{}";
        }
    }

    /// <summary>
    /// Deserializes JSON state from DevTools to a state dictionary.
    /// </summary>
    /// <param name="jsonState">JSON representation of the state.</param>
    /// <returns>State dictionary or null if deserialization fails.</returns>
    public ImmutableSortedDictionary<string, object>? DeserializeState(string jsonState)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jsonState))
            {
                return _initialState;
            }

            // First try to deserialize as a generic object dictionary
            Dictionary<string, object>? stateObject = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonState, _jsonOptions);

            if (stateObject is null)
            {
                return _initialState;
            }

            // Convert to immutable sorted dictionary
            ImmutableSortedDictionary<string, object>.Builder builder = ImmutableSortedDictionary.CreateBuilder<string, object>();

            foreach (KeyValuePair<string, object> kvp in stateObject)
            {
                // Handle JsonElement values from System.Text.Json
                object value = kvp.Value;
                if (value is JsonElement jsonElement)
                {
                    value = ConvertJsonElement(jsonElement);
                }

                builder.Add(kvp.Key, value);
            }

            return builder.ToImmutable();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DevTools state deserialization failed: {ex.Message}");
            return _initialState;
        }
    }

    /// <summary>
    /// Creates a restore state action from JSON.
    /// </summary>
    /// <param name="jsonState">JSON representation of the state.</param>
    /// <param name="actionIndex">The action index in DevTools history.</param>
    /// <returns>A restore state action or null if parsing fails.</returns>
    internal DevToolsActions.RestoreState? CreateRestoreAction(string jsonState, int actionIndex = -1)
    {
        ImmutableSortedDictionary<string, object>? stateDict = DeserializeState(jsonState);

        if (stateDict is null)
        {
            return null;
        }

        return new DevToolsActions.RestoreState(stateDict, actionIndex, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a reset action to return to initial state.
    /// </summary>
    /// <returns>A reset action.</returns>
    internal DevToolsActions.ResetToInitial CreateResetAction()
    {
        return new(DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the initial state for reset operations.
    /// </summary>
    /// <returns>The initial state dictionary or empty if not set.</returns>
    public ImmutableSortedDictionary<string, object> GetInitialState()
    {
        return _initialState ?? ImmutableSortedDictionary<string, object>.Empty;
    }

    /// <summary>
    /// Converts a JsonElement to an appropriate CLR type.
    /// </summary>
    /// <param name="element">The JsonElement to convert.</param>
    /// <returns>The converted value.</returns>
    private static object ConvertJsonElement(in JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.Deserialize<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
            JsonValueKind.Array => element.Deserialize<object[]>() ?? [],
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()
        };
    }
}
