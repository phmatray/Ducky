namespace Ducky.Blazor;

// The prerender seed {"v":1,"src":"prerender"|"pause","slices":{key: state}} (SPEC §11.4), persisted as the UTF-8 bytes of
// this envelope and written by SeedWriter. Read through BlazorWireContext: each state stays a JsonElement, which the
// store's Restore deserializes with the slice's declared type (§10). The pause fields (scope, dirty, ver) join it with
// pause seeds.
internal sealed record SeedEnvelope(Dictionary<string, object>? Slices);
