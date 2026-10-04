using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Ducky;

/// <summary>
/// One per store: the options built by <see cref="DuckyBuilder.UseJson(JsonSerializerOptions)"/>, used only through
/// <see cref="JsonTypeInfo"/> (trim and NativeAOT safe). It never throws for missing type info or for any non-fatal
/// exception raised while serializing or deserializing, including exceptions thrown by user constructors and
/// <c>init</c> accessors: it returns false or null and logs.
/// </summary>
public sealed class DuckyJson
{
    private readonly SafeLogger _logger;
    private readonly ConcurrentDictionary<Type, byte> _logged = new();

    internal DuckyJson(JsonSerializerOptions options, SafeLogger logger)
    {
        options.MakeReadOnly();
        Options = options;
        _logger = logger;
    }

    /// <summary>Gets the read-only options built by <c>UseJson</c>.</summary>
    public JsonSerializerOptions Options { get; }

    /// <summary>
    /// Resolves the type info of <paramref name="type"/> from <see cref="Options"/>, which caches hits and misses. A type
    /// whose metadata the serializer rejects (a duplicate discriminator, colliding property names, an open generic) has none.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <param name="typeInfo">The type info, or null when the resolver has none.</param>
    /// <returns>True when the resolver has type info for <paramref name="type"/>.</returns>
    public bool TryGetTypeInfo(Type type, [NotNullWhen(true)] out JsonTypeInfo? typeInfo) => TryGetTypeInfo(Options, type, out typeInfo, out _);

    /// <summary>
    /// Serializes <paramref name="value"/> with the type info of <paramref name="declaredType"/>. Fails when that type info
    /// is missing or the serializer throws (a NaN, a cyclic graph, a throwing getter); each failing type is logged at Debug
    /// once per store, and a value-dependent failure doesn't stop the next value of the same type.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="declaredType">The type whose type info is used: a slice's declared state type, or an action's runtime type.</param>
    /// <param name="json">The JSON text, or null on failure.</param>
    /// <returns>True when <paramref name="value"/> was serialized.</returns>
    public bool TrySerialize(object value, Type declaredType, [NotNullWhen(true)] out string? json) =>
        TrySerialize(value, declaredType, JsonSerializer.Serialize, out json);

    /// <summary>
    /// Serializes <paramref name="value"/> to a <see cref="JsonNode"/> with the type info of
    /// <paramref name="declaredType"/>, for sanitizers. Fails like <see cref="TrySerialize(object, Type, out string?)"/>.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="declaredType">The type whose type info is used.</param>
    /// <returns>The node, or null on failure.</returns>
    public JsonNode? ToNode(object value, Type declaredType) =>
        TrySerialize(value, declaredType, JsonSerializer.SerializeToNode, out var node) ? node : null;

    /// <summary>
    /// Deserializes <paramref name="json"/> with the type info of <paramref name="type"/>. Missing type info and every
    /// non-fatal exception (malformed JSON, a throwing constructor or <c>init</c> accessor) return false and log a Warning
    /// naming <paramref name="key"/> and the type.
    /// </summary>
    /// <param name="json">The JSON value.</param>
    /// <param name="type">The type to deserialize: a slice's declared state type.</param>
    /// <param name="value">The value, or null on failure.</param>
    /// <param name="key">The slice or storage key named in the Warning.</param>
    /// <returns>True when <paramref name="json"/> was deserialized.</returns>
    public bool TryDeserialize(JsonElement json, Type type, out object? value, string? key = null)
    {
        if (TryGetTypeInfo(Options, type, out var typeInfo, out var failure))
        {
            try
            {
                value = json.Deserialize(typeInfo);
                return true;
            }
#pragma warning disable CA1031 // justification: deserialization never throws (§10, INV-23); the Warning carries the exception
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                failure = exception;
            }
        }

        LogUndeserializable(failure, key, type);
        value = null;
        return false;
    }

    // STJ builds and configures metadata inside the lookup, and throws there for a misconfigured or invalid type; read-only
    // options cache that exception, so it fails every lookup of the type, like a miss (§10, INV-23).
    internal static bool TryGetTypeInfo(JsonSerializerOptions options, Type type, [NotNullWhen(true)] out JsonTypeInfo? typeInfo, out Exception? failure)
    {
        failure = null;
        try
        {
            return options.TryGetTypeInfo(type, out typeInfo);
        }
#pragma warning disable CA1031 // justification: a type info lookup never throws (§10, INV-23); callers report the exception
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            failure = exception;
            typeInfo = null;
            return false;
        }
    }

    // The Warning of a value that is not a state of its slice: a failed deserialization, or JSON null (§10).
    internal void LogUndeserializable(Exception? failure, string? key, Type type) => Log.Undeserializable(_logger, failure, key, type);

    // A store without UseJson: no type has type info, so every value degrades (§10).
    internal static JsonSerializerOptions NoTypeInfo() => new() { TypeInfoResolver = JsonTypeInfoResolver.Combine() };

    // Missing type info (a throwing lookup included) is final for this store, so the resolver's own cache of misses is all
    // it takes; a serializer exception fails only this call.
    private bool TrySerialize<T>(object value, Type declaredType, Func<object, JsonTypeInfo, T> serialize, [NotNullWhen(true)] out T? result)
    {
        if (TryGetTypeInfo(Options, declaredType, out var typeInfo, out var failure))
        {
            try
            {
                result = serialize(value, typeInfo)!;
                return true;
            }
#pragma warning disable CA1031 // justification: serialization never throws (§10, INV-23); callers degrade per path
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                failure = exception;
            }
        }

        if (_logged.TryAdd(declaredType, 0))
        {
            Log.Unserializable(_logger, failure, declaredType);
        }

        result = default;
        return false;
    }
}
