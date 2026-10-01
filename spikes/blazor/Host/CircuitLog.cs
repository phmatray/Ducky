using Client;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Host;

// Circuit lifecycle in /spike-log: S-5 checks the circuit stays open, S-7 that reconnect keeps it and resume replaces it.
internal sealed class CircuitLog : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken) => Log("opened", circuit);

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken) => Log("connection up", circuit);

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken) => Log("connection down", circuit);

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken) => Log("closed", circuit);

    private static Task Log(string what, Circuit circuit)
    {
        SpikeInfo.Log?.Invoke($"circuit {circuit.Id[..8]} {what}");
        return Task.CompletedTask;
    }
}
