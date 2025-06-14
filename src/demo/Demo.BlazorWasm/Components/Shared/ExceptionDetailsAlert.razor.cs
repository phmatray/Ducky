using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class ExceptionDetailsAlert
{
    private const string Separator = "•";

    [Parameter]
    [EditorRequired]
    public required Notification Notification { get; set; }

    [Parameter]
    public string SearchTerm { get; set; } = string.Empty;

    private void MarkNotificationAsRead(Guid id)
        => Dispatcher.MarkNotificationAsRead(id);
}
