using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Ducky.Generator.WebApp.Components.Shared;

public partial class GeneratedCode
{
    private MarkupString? _highlightedCode;

    [Parameter]
    [EditorRequired]
    public required string? Code { get; set; }

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            // var bytes = Encoding.UTF8.GetBytes(Code);
            // var base64 = Convert.ToBase64String(bytes);
            // DownloadLink = $"data:text/plain;base64,{base64}";
            // you could also derive this from some other param:
            // DownloadFileName = "ActionCreators.cs";
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            string highlighted = await HighlightAsync(Code);
            _highlightedCode = new MarkupString(highlighted);
            StateHasChanged();
        }

        await base.OnParametersSetAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !string.IsNullOrEmpty(Code))
        {
            string highlighted = await HighlightAsync(Code);
            _highlightedCode = new MarkupString(highlighted);
            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task<string> HighlightAsync(string code)
        => await Js.InvokeAsync<string>("highlight", code);

    private async Task CopyAsync()
        => await Js.InvokeVoidAsync("navigator.clipboard.writeText", Code);
}
