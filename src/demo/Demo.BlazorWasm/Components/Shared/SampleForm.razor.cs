using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class SampleForm
{
    private MudForm _login = null!;

    [Parameter]
    [EditorRequired]
    public EventCallback OnLogin { get; set; }
}
