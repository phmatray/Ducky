using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Host;

// SPIKE_MODES picks the render modes the app configures: server (single mode), wasm (single mode) or auto (both, the
// Interactive Auto template). Interactivity is global, on Routes.
internal static class Modes
{
    public static readonly string Name = Environment.GetEnvironmentVariable("SPIKE_MODES") ?? "auto";

    public static bool Server => Name is "server" or "auto";

    public static bool Wasm => Name is "wasm" or "auto";

    public static IComponentRenderMode Global => Name switch
    {
        "server" => RenderMode.InteractiveServer,
        "wasm" => RenderMode.InteractiveWebAssembly,
        _ => RenderMode.InteractiveAuto,
    };
}
