using System.Text.Json;

namespace Ducky.Blazor;

// SPEC §11.8: the inbound wire shape ducky.js forwards, read through BlazorWireContext: {type, payload: {type, actionId?,
// index?, nextLiftedState?}}. Only the members a command needs are read; the extension's others are skipped.
internal sealed record DevToolsMessage(string? Type, DevToolsCommand? Payload);

internal sealed record DevToolsCommand(string? Type, long? ActionId, long? Index, DevToolsLiftedState? NextLiftedState);

internal sealed record DevToolsLiftedState(IReadOnlyList<DevToolsComputedState>? ComputedStates);

internal sealed record DevToolsComputedState(Dictionary<string, JsonElement>? State);
