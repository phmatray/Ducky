namespace Ducky.Blazor;

// The prerender seed {"v":1,"src":"prerender"|"pause","dirty":[key, …]?,"ver":{key: version}?,"slices":{key: state}}
// (SPEC §11.4), persisted as the UTF-8 bytes of this envelope and written by SeedWriter. Read through BlazorWireContext:
// each state stays a JsonElement, which the store's Restore deserializes with the slice's declared type (§10). The scope
// hash joins it with scoped pause seeds (M12-07).
internal sealed record SeedEnvelope(Dictionary<string, object>? Slices)
{
    /// <summary>The <see cref="PersistOptions.Version"/> of each persisted key the seed carries.</summary>
    public Dictionary<string, int> Ver { get; set; } = [];

    /// <summary>A pause seed's browser-storage keys whose value differed from the last known stored payload.</summary>
    public string[] Dirty { get; set; } = [];
}
