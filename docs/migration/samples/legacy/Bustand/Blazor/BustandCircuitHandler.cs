using System.Diagnostics;
using Bustand.Persistence;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Bustand.Blazor;

/// <summary>
/// Circuit handler that manages storage availability during Blazor Server circuit lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// In Blazor Server, circuits can disconnect and reconnect (e.g., network issues, tab backgrounding).
/// When this happens, the scoped services (including stores) persist in memory, but JS interop
/// may need to be re-established.
/// </para>
/// <para>
/// This handler:
/// - Marks storage as unavailable on disconnect
/// - Re-marks storage as available on reconnect (which triggers OnAvailabilityChanged)
/// - Allows stores to re-restore state from browser storage on reconnect
/// </para>
/// <para>
/// This handler has no effect in WASM mode (no circuits in WASM).
/// </para>
/// </remarks>
public class BustandCircuitHandler : CircuitHandler
{
    private readonly IBrowserStorage _storage;

    /// <summary>
    /// Creates a new circuit handler instance.
    /// </summary>
    /// <param name="storage">The browser storage service.</param>
    public BustandCircuitHandler(IBrowserStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Called when a circuit connection is established.
    /// </summary>
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[Bustand] Circuit opened: {circuit.Id}");
        // Note: We don't call SetAvailable here because JS interop isn't ready yet.
        // BustandInitializer handles the first SetAvailable() call after first render.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a circuit connection is closed (user navigates away, closes tab, etc.).
    /// </summary>
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[Bustand] Circuit closed: {circuit.Id}");
        // Mark storage as unavailable since JS interop is no longer available
        _storage.SetUnavailable();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a circuit connection is re-established after a temporary disconnection.
    /// </summary>
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[Bustand] Circuit connection up: {circuit.Id}");
        // Re-mark storage as available - this will trigger OnAvailabilityChanged
        // which allows stores to re-restore their state from storage
        _storage.SetAvailable();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a circuit connection is temporarily lost.
    /// </summary>
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[Bustand] Circuit connection down: {circuit.Id}");
        // Mark storage as unavailable since JS interop is interrupted
        _storage.SetUnavailable();
        return Task.CompletedTask;
    }
}
