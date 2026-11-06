namespace Ducky.Blazor;

// SPEC §11.8: the devtoolsConnect config, pre-serialized through BlazorWireContext. The stack comes from .NET, so the
// extension's own trace is always off. devtoolsConnect has no budget argument, so the inline budget
// (BlazorOptions.InlinePayloadBytes) rides here as maxInlineBytes; ducky.js rejects a config without it and strips it
// before the options reach the extension (§11.9).
internal sealed record DevToolsConfig(string Name, int MaxAge, bool Trace, int MaxInlineBytes);

// SPEC §10, §11.8: the DevTools projections of the failure actions; the exception itself is never serialized.
internal sealed record ReducerFailure(string Type, string Message, string? SliceKey);

internal sealed record EffectFailure(string Type, string Message, string EffectType);
