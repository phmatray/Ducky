using Microsoft.JSInterop;

namespace Ducky.Blazor;

/// <summary>One argument that may cross into JS (SPEC §10, INV-23): a string, int, long, bool or DotNetObjectReference.</summary>
internal readonly struct JsArg
{
    private JsArg(object? value) => Value = value;

    internal object? Value { get; }

    public static implicit operator JsArg(string? value) => new(value);

    public static implicit operator JsArg(int value) => new(value);

    public static implicit operator JsArg(long value) => new(value);

    public static implicit operator JsArg(bool value) => new(value);

    internal static JsArg Ref<T>(DotNetObjectReference<T> value)
        where T : class => new(value);
}
