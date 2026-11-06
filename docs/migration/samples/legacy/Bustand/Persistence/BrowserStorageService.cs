using System.Diagnostics;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Bustand.Persistence;

/// <summary>
/// JS interop implementation of browser storage.
/// Handles both localStorage and sessionStorage with mode-aware async operations.
/// </summary>
public class BrowserStorageService : IBrowserStorage
{
    private readonly IJSRuntime _jsRuntime;
    private readonly JsonSerializerOptions _jsonOptions;
    private volatile bool _isAvailable;

    /// <summary>
    /// Creates a new browser storage service.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop calls.</param>
    /// <param name="jsonOptions">JSON serialization options. If null, uses defaults.</param>
    public BrowserStorageService(IJSRuntime jsRuntime, JsonSerializerOptions? jsonOptions = null)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public bool IsAvailable => _isAvailable;

    /// <inheritdoc />
    public event Action? OnAvailabilityChanged;

    /// <inheritdoc />
    public void SetAvailable()
    {
        var wasAvailable = _isAvailable;
        _isAvailable = true;

        // Raise event if this is a state change (new availability or re-availability after reset)
        if (!wasAvailable)
        {
            OnAvailabilityChanged?.Invoke();
            Debug.WriteLine("[Bustand] Storage availability changed: now available");
        }
    }

    /// <inheritdoc />
    public void SetUnavailable()
    {
        if (_isAvailable)
        {
            _isAvailable = false;
            Debug.WriteLine("[Bustand] Storage marked as unavailable (circuit disconnect)");
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, StorageType storageType) where T : class
    {
        if (!_isAvailable)
        {
            Debug.WriteLine($"[Bustand] Storage not available, returning default for key '{key}'");
            return default;
        }

        try
        {
            var storageName = GetStorageName(storageType);
            var json = await _jsRuntime.InvokeAsync<string?>(
                $"{storageName}.getItem",
                key);

            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[Bustand] Failed to deserialize state for '{key}': {ex.Message}");
            return default;
        }
        catch (JSException ex)
        {
            Debug.WriteLine($"[Bustand] JS interop failed for '{key}': {ex.Message}");
            return default;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            // Prerender scenario - JS not available yet
            Debug.WriteLine($"[Bustand] JS interop unavailable (prerender) for '{key}'");
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, StorageType storageType) where T : class
    {
        if (!_isAvailable)
        {
            Debug.WriteLine($"[Bustand] Storage not available, skipping write for key '{key}'");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);

            // Warn on large state (RESEARCH.md recommends 100KB for WASM, 30KB for Server)
            if (json.Length > 100_000)
            {
                Debug.WriteLine(
                    $"[Bustand] Warning: Persisted state for '{key}' is {json.Length / 1024}KB. " +
                    "Consider reducing state size for better performance.");
            }

            var storageName = GetStorageName(storageType);
            await _jsRuntime.InvokeVoidAsync(
                $"{storageName}.setItem",
                key,
                json);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[Bustand] Failed to serialize state for '{key}': {ex.Message}. " +
                "Ensure all state properties are serializable.");
        }
        catch (JSException ex)
        {
            Debug.WriteLine($"[Bustand] JS interop failed for '{key}': {ex.Message}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            Debug.WriteLine($"[Bustand] JS interop unavailable (prerender) for '{key}'");
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, StorageType storageType)
    {
        if (!_isAvailable)
            return;

        try
        {
            var storageName = GetStorageName(storageType);
            await _jsRuntime.InvokeVoidAsync($"{storageName}.removeItem", key);
        }
        catch (JSException ex)
        {
            Debug.WriteLine($"[Bustand] JS interop failed removing '{key}': {ex.Message}");
        }
    }

    private static string GetStorageName(StorageType storageType) =>
        storageType switch
        {
            StorageType.Local => "localStorage",
            StorageType.Session => "sessionStorage",
            _ => throw new ArgumentOutOfRangeException(nameof(storageType))
        };
}
