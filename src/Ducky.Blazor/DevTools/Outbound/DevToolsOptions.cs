using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Ducky.Blazor;

/// <summary>
/// The Redux DevTools bridge's options: one instance per <see cref="DuckyBuilder"/>, to which every
/// <see cref="DuckyBlazorBuilderExtensions.UseDevTools"/> configure delegate is applied in call order.
/// </summary>
public sealed class DevToolsOptions
{
    /// <summary>The instance name the extension shows. Default <c>"Ducky"</c>.</summary>
    public string Name { get; set; } = "Ducky";

    /// <summary>How many entries the history keeps, and the extension's <c>maxAge</c>. Default 50.</summary>
    public int MaxAge { get; set; } = 50;

    /// <summary>Decides, per processed action, whether it is sent; <see langword="null"/> (the default) sends every action.</summary>
    public Func<ActionContext, bool>? Filter { get; set; }

    /// <summary>
    /// Rewrites each action payload before it is sent: <c>(actionType, payload) → payload</c>. The payload is
    /// <see langword="null"/> when the action can't be serialized. Runs on every send.
    /// </summary>
    public Func<string, JsonNode?, JsonNode?>? ActionSanitizer { get; set; }

    /// <summary>
    /// Rewrites each slice's state before it is sent: <c>(sliceKey, state) → state</c>. Runs on <c>@@INIT</c> and on every
    /// send, for every slice, Ducky's own <c>@ducky/*</c> slices included.
    /// </summary>
    public Func<string, JsonNode?, JsonNode?>? StateSanitizer { get; set; }

    /// <summary>Whether each action carries the .NET stack it was reduced on. Default off.</summary>
    public bool Trace { get; set; }

    /// <summary>How many frames a <see cref="Trace"/> stack keeps at most. Default 10.</summary>
    public int TraceLimit { get; set; } = 10;

    // SPEC §10: the seam over DiagnosticMethodInfo.Create, null when metadata was trimmed; Ducky.Blazor.Tests sets it to
    // cover the <unknown> frame under JIT.
    internal Func<StackFrame, DiagnosticMethodInfo?> DescribeFrame { get; set; } = DiagnosticMethodInfo.Create;
}
