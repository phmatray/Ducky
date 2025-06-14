using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class JsonMarkup
{
    private MarkupString _dataColorized;

    [Parameter]
    [EditorRequired]
    public required string Data { get; set; }

    protected override void OnInitialized()
    {
        _dataColorized = (MarkupString)JsonColorizer.ColorizeJson(Data);
    }
}
