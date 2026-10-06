using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

// Ducky.Blazor log events (SPEC §9): EventIds 2000-2999, types and keys only, never values. Assigned by SPEC §6.3 and §11
// (the source of truth); M5-02 uses 2000, M5-03 (SubscriptionCore) 2001-2003, M6-02 2030-2033 (the envelope reader).
// 2010 (a seed that is not an envelope) is NOT assigned by SPEC: M6-04 took it pending an owner note so §11.4 assigns it.
// M6-06 uses 2022 (§11.4) and 2025, which SPEC does not assign either (the browser seed restore skipped after an abort or a dispose).
// M6-05 uses 2014 and 2021 (§11.4) and takes, on the same terms, 2011 (the include predicate's Debug log, which §11.4
// leaves unnumbered) and 2012 (a seed write that failed, which §11.4 doesn't name).
// M6-09 uses 2023 (§11.5: a stored value above MaxPayloadBytes) and takes 2026, not assigned by SPEC, for the per-key
// Warning of a failed pull (§11.5 step 4: "a failed pull is caught per key", which §11.5 leaves unnumbered).
internal static partial class Log
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "JS interop call '{Identifier}' interrupted: circuit disconnected or interop timed out")]
    internal static partial void InteropInterrupted(SafeLogger logger, Exception exception, string identifier);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Scheduling a component's change check on its renderer failed; the next commit schedules it again")]
    internal static partial void ScheduleFailed(SafeLogger logger, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "A component's change check failed; the component re-renders so that its render surfaces the error")]
    internal static partial void EvaluateFailed(SafeLogger logger, Exception exception);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Requesting a component's re-render after a change failed; the next commit checks it again")]
    internal static partial void RenderRequestFailed(SafeLogger logger, Exception exception);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "The prerender seed is not a seed envelope; nothing was restored from it")]
    internal static partial void UnreadableSeed(SafeLogger logger, Exception? exception);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Slice '{Key}' was left out of the prerender seed: its include predicate is false for the current state")]
    internal static partial void SeedSliceExcluded(SafeLogger logger, string key);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning, Message = "The prerender seed could not be written: no seed was persisted, and the interactive side loads again")]
    internal static partial void SeedWriteFailed(SafeLogger logger, Exception exception);

    [LoggerMessage(EventId = 2014, Level = LogLevel.Warning, Message = "The store was not idle within PrerenderIdleTimeout ({Timeout}): no prerender seed was persisted, and the interactive side loads again")]
    internal static partial void SeedIdleTimeout(SafeLogger logger, TimeSpan timeout);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Warning, Message = "Slice '{Key}' was left out of the prerender seed: its include predicate threw, its state can't be serialized, or it would push the seed's estimated wire size past PrerenderSeedMaxWireBytes; the interactive side loads it again")]
    internal static partial void SeedSliceOmitted(SafeLogger logger, string key, Exception? exception);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Warning, Message = "No prerender seed arrived within PrerenderSeedWaitTimeout ({Timeout}): don't await a preload before RunAsync, or render <DuckyInitializer> (or a Ducky component) first")]
    internal static partial void SeedWaitTimedOut(SafeLogger logger, TimeSpan timeout);

    [LoggerMessage(EventId = 2023, Level = LogLevel.Warning, Message = "The value of '{Key}' is {Length} bytes, above MaxPayloadBytes ({MaxPayloadBytes}); read as not found until the next write replaces it")]
    internal static partial void PayloadTooLarge(SafeLogger logger, string key, long length, int maxPayloadBytes);

    [LoggerMessage(EventId = 2025, Level = LogLevel.Debug, Message = "Init ended (overflow abort or store disposed) before the browser's prerender seed settled; the seed is not restored, the live store is authoritative")]
    internal static partial void SeedSkippedAfterAbort(SafeLogger logger);

    [LoggerMessage(EventId = 2026, Level = LogLevel.Warning, Message = "Pulling the value of '{Key}' through storageGetStream failed; read as not found")]
    internal static partial void PullFailed(SafeLogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 2030, Level = LogLevel.Debug, Message = "The value of '{Key}' is not a 2.x envelope (1.x or foreign data); discarded")]
    internal static partial void NotAnEnvelope(SafeLogger logger, string key);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Warning, Message = "The envelope of '{Key}' has version {Found}, newer than version {Version}; read as not found")]
    internal static partial void NewerEnvelope(SafeLogger logger, string key, int found, int version);

    [LoggerMessage(EventId = 2032, Level = LogLevel.Debug, Message = "The envelope of '{Key}' is older than its MaxAge; discarded")]
    internal static partial void ExpiredEnvelope(SafeLogger logger, string key);

    [LoggerMessage(EventId = 2033, Level = LogLevel.Warning, Message = "The envelope of '{Key}' could not be read as {Type} (malformed, a migration failed or the state is null); the state is kept")]
    internal static partial void UnreadableEnvelope(SafeLogger logger, Exception? exception, string key, Type type);
}
