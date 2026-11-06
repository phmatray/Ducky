namespace Ducky.Blazor;

/// <summary>Where a writer's loop is when it invokes <see cref="BlazorOptions.AfterDeferHook"/> (SPEC §11.5).</summary>
internal enum DeferPoint
{
    /// <summary>After the snapshot read that decided the skip, before the key is added to <c>_deferred</c>.</summary>
    BeforeAdd,

    /// <summary>After the add, before the recheck.</summary>
    AfterAdd,

    /// <summary>In an attempt, under the writer's gate, between the reads of its removal and final-snapshot queues' heads.</summary>
    BetweenPeeks,

    /// <summary>In an attempt, after the loop read the heads of its removal and final-snapshot queues, before it acts on them.</summary>
    Peeked,

    /// <summary>On the drainer at a reset, under the writer's gate, right after the final snapshot is enqueued.</summary>
    PostingFinal,

    /// <summary>In a write, after the signals and clears are read, before the snapshot.</summary>
    BeforeSnapshot,

    /// <summary>In a write, after the snapshot, before it is compared with the baseline.</summary>
    AfterSnapshot,
}
