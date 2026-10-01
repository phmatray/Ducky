using Ducky;
using Ducky.Blazor;

namespace Ducky.PackageSmoke.BlazorOnly;

/// <summary>Uses a Ducky type and a Ducky.Blazor type through the Ducky.Blazor reference alone.</summary>
[ActionType("smoke/blazor-only")]
public static class Consumer
{
    /// <summary>Creates a library action.</summary>
    public static PersistenceFailed Failed() => new("smoke", nameof(Consumer), "blazor-only");
}
