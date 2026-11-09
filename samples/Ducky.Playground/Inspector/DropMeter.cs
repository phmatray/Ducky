using System.Diagnostics.Metrics;

namespace Ducky.Playground.Inspector;

// Dropped work never reaches a middleware, but the store counts it on its Meter (SPEC §9): ducky.effect.dropped for an
// Exhaust drop, ducky.dispatch.dropped (tagged ducky.drop.reason) for a superseded run's dispatch. A MeterListener hears
// both, synchronously, while the store processes the action that caused them.
public sealed class DropMeter : IDisposable
{
    private readonly MeterListener _listener = new();

    public DropMeter(Tape tape)
    {
        _listener.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Ducky" && instrument.Name is "ducky.effect.dropped" or "ducky.dispatch.dropped")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "ducky.effect.dropped")
            {
                tape.EffectRunDropped();
                return;
            }

            var reason = "run";
            foreach (var tag in tags)
            {
                if (tag.Key == "ducky.drop.reason" && tag.Value is string value)
                {
                    reason = value;
                }
            }
            tape.DispatchDropped(reason == "depth" ? "past MaxDispatchDepth" : "its Switch run was superseded");
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();
}
