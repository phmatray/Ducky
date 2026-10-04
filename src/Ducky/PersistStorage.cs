namespace Ducky;

/// <summary>Where a persisted slice is stored. Consumed by Ducky.Blazor; declared here so <see cref="PersistAttribute"/> can use it.</summary>
public enum PersistStorage
{
    /// <summary>The browser's localStorage.</summary>
    Local,

    /// <summary>The browser's sessionStorage.</summary>
    Session,

    /// <summary>The server's <c>IDistributedCache</c>.</summary>
    Server,
}
