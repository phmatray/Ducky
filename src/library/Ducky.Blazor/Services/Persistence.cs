// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Blazored.LocalStorage;
using R3;

namespace Ducky.Blazor.Services;

/// <summary>
/// A service that provides persistence for Ducky root state.
/// </summary>
public sealed class Persistence : IDisposable
{
    private readonly IRootStateSerializer _rootStateSerializer;
    private readonly ILocalStorageService _localStorage;
    private readonly IDisposable _subscription;
    private bool _disposed;

    private const string RootStateKey = "root-state";

    /// <summary>
    /// Initializes a new instance of the <see cref="Persistence"/> class.
    /// </summary>
    /// <param name="store">The store that manages the application state.</param>
    /// <param name="rootStateSerializer">The serializer used to serialize and deserialize the root state.</param>
    /// <param name="localStorage">The local storage service used to persist the root state.</param>
    public Persistence(
        DuckyStore store,
        IRootStateSerializer rootStateSerializer,
        ILocalStorageService localStorage)
    {
        _rootStateSerializer = rootStateSerializer;
        _localStorage = localStorage;

        _subscription = store.RootStateObservable
            .Subscribe(rootState => _ = PersistAsync(rootState));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _subscription.Dispose();
        }

        _disposed = true;
    }

    private async Task PersistAsync(IRootState rootState)
    {
        await _localStorage.SetItemAsync(RootStateKey, rootState);
    }

    // private async Task<RootState> RehydrateAsync()
    // {
    //     throw new NotImplementedException();
    // }
}
