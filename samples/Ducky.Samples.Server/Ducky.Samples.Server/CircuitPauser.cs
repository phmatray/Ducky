using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Ducky.Samples.Server;

/// <summary>
/// Tracks the open circuits so a test can pause them from the server, as a host does on shutdown
/// (<c>Circuit.RequestCircuitPauseAsync</c>, ASP.NET Core 11).
/// </summary>
public sealed class CircuitPauser : CircuitHandler
{
    private static readonly Dictionary<string, Circuit> _open = [];

    /// <summary>How many circuits are open.</summary>
    public static int Open
    {
        get
        {
            lock (_open)
            {
                return _open.Count;
            }
        }
    }

    /// <summary>Asks every open circuit's client to start the graceful pause; true when all were accepted.</summary>
    public static async Task<bool> RequestPauseAsync(CancellationToken cancellationToken)
    {
        Circuit[] circuits;
        lock (_open)
        {
            circuits = [.. _open.Values];
        }

        var accepted = circuits.Length > 0;
        foreach (var circuit in circuits)
        {
            accepted &= await circuit.RequestCircuitPauseAsync(cancellationToken);
        }

        return accepted;
    }

    /// <inheritdoc />
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        lock (_open)
        {
            _open[circuit.Id] = circuit;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        lock (_open)
        {
            _open.Remove(circuit.Id);
        }

        return Task.CompletedTask;
    }
}
