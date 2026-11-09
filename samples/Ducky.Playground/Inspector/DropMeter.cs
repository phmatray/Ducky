// Dropped work never reaches a middleware, but the store counts it on its "Ducky"
// Meter, so a MeterListener puts each drop on the inspector tape.
using System.Diagnostics.Metrics;

namespace Ducky.Playground.Inspector;

public sealed class DropMeter : IDisposable
{
    // An Exhaust effect skipped a run.
    private const string EffectDropped = "ducky.effect.dropped";

    // A superseded Switch run tried to dispatch, or a dispatch went past
    // MaxDispatchDepth.
    private const string DispatchDropped = "ducky.dispatch.dropped";

    private readonly MeterListener _listener = new();

    // The listener hears each measurement synchronously, while the store is still
    // processing the action that caused the drop.
    public DropMeter(Tape tape)
    {
        _listener.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Ducky"
                && instrument.Name is EffectDropped or DispatchDropped)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == EffectDropped)
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
            tape.DispatchDropped(reason == "depth"
                ? "past MaxDispatchDepth"
                : "its Switch run was superseded");
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();
}
