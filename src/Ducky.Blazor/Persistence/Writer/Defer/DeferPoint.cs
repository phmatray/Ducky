namespace Ducky.Blazor;

/// <summary>Where a writer is in its deferral when it invokes <see cref="BlazorOptions.AfterDeferHook"/> (SPEC §11.5).</summary>
internal enum DeferPoint
{
    /// <summary>After the snapshot read that decided the skip, before the key is added to <c>_deferred</c>.</summary>
    BeforeAdd,

    /// <summary>After the add, before the recheck.</summary>
    AfterAdd,
}
