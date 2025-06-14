using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class SearchBox
{
    [Parameter]
    public string Label { get; set; } = "Search";

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public int DebounceInterval { get; set; } = 300;

    [Parameter]
    public EventCallback<string> OnSearch { get; set; }

    private string SearchTerm { get; set; } = string.Empty;

    private async Task OnDebounceIntervalElapsedAsync(string value)
    {
        await OnSearch.InvokeAsync(value);
    }
}
