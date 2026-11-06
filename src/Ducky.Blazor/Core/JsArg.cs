using System.Diagnostics.CodeAnalysis;
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

    // DotNetObjectReference<TValue> keeps TValue's public methods ([JSInvokable]); without the matching annotation ILLink
    // reports IL2091 here in a trimmed publish (the trim analyzer does not, WasmTrimmedSmoke's publish does, INV-24).
    internal static JsArg Ref<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(DotNetObjectReference<T> value)
        where T : class => new(value);
}
