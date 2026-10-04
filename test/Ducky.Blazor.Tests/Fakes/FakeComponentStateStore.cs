using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Ducky.Blazor.Tests.Fakes;

/// <summary>
/// The store behind a real <see cref="Microsoft.AspNetCore.Components.Infrastructure.ComponentStatePersistenceManager"/>
/// (SPEC §17.2): <see cref="PersistentComponentState"/> is never faked. A test restores with
/// <c>manager.RestoreStateAsync(store)</c> at the point it chooses and persists with <c>manager.PersistStateAsync(store, renderer)</c>.
/// <see cref="SupportsRenderMode"/> accepts only Interactive Auto and records what it receives, so a seed is persisted iff
/// its registration's mode is Interactive Auto.
/// </summary>
internal sealed class FakeComponentStateStore : IPersistentComponentStateStore
{
    /// <summary>What a restore reads, and what a persist replaces whole, as a real store does.</summary>
    public Dictionary<string, byte[]> State { get; } = [];

    public List<IComponentRenderMode> RenderModes { get; } = [];

    public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() =>
        Task.FromResult<IDictionary<string, byte[]>>(new Dictionary<string, byte[]>(State));

    public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
    {
        State.Clear();
        foreach (var (key, value) in state)
        {
            State[key] = value;
        }

        return Task.CompletedTask;
    }

    public bool SupportsRenderMode(IComponentRenderMode renderMode)
    {
        RenderModes.Add(renderMode);
        return renderMode is InteractiveAutoRenderMode;
    }
}
