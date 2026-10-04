using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ducky;

// SPEC §6.3 and §9: tracing through the one stateless static ActivitySource, metrics only through the IMeterFactory the
// container offers (no factory, no Meter: nothing static, INV-22). ActivityListener and MeterListener callbacks run
// synchronously inside these calls and are user code, so each call is wrapped: a throw is logged (Warning 1017) and the
// step goes on (INV-03); only OutOfMemoryException (fatal, §10) gets through, as in SafeLogger.
#pragma warning disable CA1031 // justification: every non-fatal listener failure is swallowed by design (§6.3, INV-03)
internal sealed class SafeTelemetry
{
    private static readonly ActivitySource _source = new("Ducky");

    private readonly SafeLogger _logger;

    internal SafeTelemetry(SafeLogger logger, IMeterFactory? meterFactory)
    {
        _logger = logger;
        var meter = meterFactory?.Create("Ducky");
        DispatchDuration = meter?.CreateHistogram<double>("ducky.dispatch.duration", "ms");
        DispatchDropped = meter?.CreateCounter<long>("ducky.dispatch.dropped");
        EffectFailures = meter?.CreateCounter<long>("ducky.effect.failures");
        EffectDropped = meter?.CreateCounter<long>("ducky.effect.dropped");
    }

    internal Histogram<double>? DispatchDuration { get; }

    // Tagged ducky.drop.reason: depth (step 1) or run (step 3 and the call-time run check), INV-02.
    internal Counter<long>? DispatchDropped { get; }

    internal Counter<long>? EffectFailures { get; }

    // Exhaust drops (§6.6).
    internal Counter<long>? EffectDropped { get; }

    // The ducky.dispatch span of p, parented on the producer's activity (§6.4 step 2); null when nobody samples it. A
    // throwing ActivityStarted runs after Start made the span current: Current goes back to what it was (null for a
    // stopped one, which the setter rejects), so nothing created while processing hangs under a span never stopped.
    internal Activity? StartActivity(Pending p, string actionType)
    {
        var previous = Activity.Current;
        try
        {
            return _source.StartActivity("ducky.dispatch", ActivityKind.Internal, p.ParentActivity)
                ?.SetTag("ducky.action.type", actionType)
                .SetTag("ducky.origin", p.Origin.ToString())
                .SetTag("ducky.depth", p.Depth)
                .SetTag("ducky.correlation_id", p.CorrelationId);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Activity.Current = previous is { IsStopped: true } ? null : previous;
            Log.TelemetryFailed(_logger, ex, nameof(StartActivity));
            return null;
        }
    }

    internal void Stop(Activity? activity)
    {
        try
        {
            activity?.Stop();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.TelemetryFailed(_logger, ex, nameof(Stop));
        }
    }

    internal void Add(Counter<long>? counter, params ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            counter?.Add(1, tags);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.TelemetryFailed(_logger, ex, nameof(Add));
        }
    }

    internal void Record(Histogram<double>? histogram, double value)
    {
        try
        {
            histogram?.Record(value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.TelemetryFailed(_logger, ex, nameof(Record));
        }
    }
}
#pragma warning restore CA1031
