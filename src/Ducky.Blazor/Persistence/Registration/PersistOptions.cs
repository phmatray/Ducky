using System.Text.Json.Nodes;

namespace Ducky.Blazor;

/// <summary>
/// How one slice is persisted: one instance per slice, to which every
/// <see cref="DuckyBlazorBuilderExtensions.Persist{TSlice}"/> configure delegate is applied in call order, so the last
/// value written to a property wins.
/// </summary>
public sealed class PersistOptions
{
    private readonly Dictionary<int, Func<JsonNode, JsonNode>> _migrations = [];

    /// <summary>Where the slice is kept. Default <see cref="PersistStorage.Local"/>.</summary>
    public PersistStorage Storage { get; set; } = PersistStorage.Local;

    /// <summary>
    /// The version written with the state. Default 1. An older envelope is migrated through <see cref="Migrate"/>, so every
    /// step from 1 to <c>Version - 1</c> is required (DUCKY311); a newer one is ignored.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>Discards a stored state written longer ago than this. <see langword="null"/> (the default) keeps it.</summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// How long a change waits before it is written. <see langword="null"/> (the default): no wait in the browser and for
    /// <see cref="PersistStorage.Server"/>, 250 ms for browser storage written from the server.
    /// </summary>
    public TimeSpan? Debounce { get; set; }

    /// <summary>Applies the slice's changes from other tabs. Valid only with <see cref="PersistStorage.Local"/> (DUCKY312).</summary>
    public bool SyncAcrossTabs { get; set; }

    /// <summary>The steps by their from-version, each one version forward.</summary>
    internal IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> Migrations => _migrations;

    /// <summary>
    /// Adds the step that turns a stored state of version <paramref name="fromVersion"/> into version
    /// <paramref name="fromVersion"/> + 1. A later step for the same version replaces it.
    /// </summary>
    /// <param name="fromVersion">The version the step reads.</param>
    /// <param name="step">Turns the stored state into the next version's.</param>
    /// <returns>These options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="step"/> is <see langword="null"/>.</exception>
    public PersistOptions Migrate(int fromVersion, Func<JsonNode, JsonNode> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _migrations[fromVersion] = step;
        return this;
    }
}
