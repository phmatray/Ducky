using Microsoft.Extensions.DependencyInjection;

namespace Client;

// Process-wide facts each host's Program.cs records, read by the pages. Throwaway prototype, so statics are fine.
public static class SpikeInfo
{
    public static IReadOnlyList<ServiceDescriptor> Descriptors { get; set; } = [];

    public static IServiceProvider? Root { get; set; }

    // Server only: appended to the /spike-log endpoint.
    public static Action<string>? Log { get; set; }

    public static string Lifetime(Type type, IEnumerable<ServiceDescriptor>? descriptors = null)
    {
        var d = (descriptors ?? Descriptors).LastOrDefault(s => s.ServiceType == type);
        return d is null ? "not registered" : $"{d.Lifetime} ({d.ImplementationType?.Name ?? (d.ImplementationInstance is null ? "factory" : "instance")})";
    }

    // Browser only: WebAssemblyHost.Services (Program.cs), compared with Root, which SpikeStore records.
    public static IServiceProvider? HostServices { get; set; }
}
