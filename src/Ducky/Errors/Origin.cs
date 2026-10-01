namespace Ducky;

/// <summary>Where an action comes from.</summary>
public enum Origin
{
    /// <summary>Dispatched by application code outside an effect run.</summary>
    Local,

    /// <summary>Dispatched by an effect run.</summary>
    Effect,

    /// <summary>A restore from persisted state.</summary>
    Hydration,

    /// <summary>A restore from another browser tab.</summary>
    CrossTab,

    /// <summary>A restore from Redux DevTools time travel.</summary>
    DevTools,

    /// <summary>Dispatched by Ducky or an extension package.</summary>
    System,
}
