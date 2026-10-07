using System.Diagnostics.Metrics;

namespace Ducky.Blazor;

// SPEC §6.3 and §9: Ducky.Blazor's ducky.persistence.* metrics, only through the IMeterFactory the container offers (no
// factory, no Meter). MeterListener callbacks run synchronously inside these calls and are user code, so each call is
// wrapped: a throw is logged (Warning 2024) and the step goes on (INV-03); only OutOfMemoryException (fatal, §10) gets
// through, as in SafeLogger. Ducky.Blazor's own copy: shipped assemblies share no internals (R-PKG-2).
#pragma warning disable CA1031 // justification: every non-fatal listener failure is swallowed by design (§6.3, INV-03)
internal sealed class SafeTelemetry
{
    private readonly SafeLogger _logger;

    internal SafeTelemetry(SafeLogger logger, IMeterFactory? meterFactory)
    {
        _logger = logger;
        var meter = meterFactory?.Create("Ducky");
        Saves = meter?.CreateCounter<long>("ducky.persistence.saves");
        SaveDuration = meter?.CreateHistogram<double>("ducky.persistence.save.duration", "ms");
        Lost = meter?.CreateCounter<long>("ducky.persistence.lost");
    }

    // Successful writes (§11.5).
    internal Counter<long>? Saves { get; }

    internal Histogram<double>? SaveDuration { get; }

    // Keys still dirty when the dispose flush ends (§11.5).
    internal Counter<long>? Lost { get; }

    internal void Add(Counter<long>? counter)
    {
        try
        {
            counter?.Add(1);
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
