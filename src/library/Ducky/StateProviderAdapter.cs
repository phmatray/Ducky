// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Adapter that wraps an IStateProvider to implement IRootState for backward compatibility.
/// This is a temporary bridge to support the transition from IRootState to IStateProvider.
/// </summary>
public sealed class StateProviderAdapter : IRootState
{
    private readonly IStateProvider _stateProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateProviderAdapter"/> class.
    /// </summary>
    /// <param name="stateProvider">The state provider to wrap.</param>
    public StateProviderAdapter(IStateProvider stateProvider)
    {
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
    }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        IReadOnlyDictionary<string, object> slices = _stateProvider.GetAllSlices();
        return slices.ToImmutableSortedDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <inheritdoc />
    public ImmutableSortedSet<string> GetKeys()
    {
        return _stateProvider.GetSliceKeys().ToImmutableSortedSet();
    }

    /// <inheritdoc />
    public TState GetSlice<TState>(string key) where TState : notnull
    {
        return _stateProvider.GetSliceByKey<TState>(key);
    }

    /// <inheritdoc />
    public TState GetSlice<TState>() where TState : notnull
    {
        return _stateProvider.GetSlice<TState>();
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _stateProvider.HasSliceByKey(key);
    }
}
