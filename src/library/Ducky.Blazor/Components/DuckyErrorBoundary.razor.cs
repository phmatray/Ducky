using Ducky.Pipeline;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Components;

/// <summary>
/// An error boundary component for Ducky Blazor applications that integrates with the Ducky exception handling system.
/// </summary>
public partial class DuckyErrorBoundary
{
    /// <summary>
    /// Gets or sets the child content to be rendered inside the error boundary.
    /// </summary>
    [Parameter]
    public new RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the error content to be rendered when an exception occurs.
    /// If not provided, a default error UI will be displayed.
    /// </summary>
    [Parameter]
    public new RenderFragment<Exception>? ErrorContent { get; set; }

    /// <summary>
    /// Gets or sets whether to show detailed error information in the default error UI.
    /// </summary>
    [Parameter]
    public bool ShowDetails { get; set; }

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled exception occurred in Blazor component");

        // Create error event args
        ActionErrorEventArgs errorEventArgs = new(exception, "BlazorComponent", new ActionContext("BlazorError"));

        // Allow exception handlers to handle the exception
        var isHandled = false;
        foreach (IExceptionHandler handler in ExceptionHandlers)
        {
            try
            {
                if (handler.HandleActionError(errorEventArgs))
                {
                    isHandled = true;
                    break;
                }
            }
            catch (Exception handlerException)
            {
                Logger.LogError(
                    handlerException,
                    "Exception handler {HandlerType} threw an exception while handling {OriginalExceptionType}",
                    handler.GetType().Name,
                    exception.GetType().Name);
            }
        }

        // Publish the error event
        ActionErrorEventArgs finalEventArgs =
            new(exception, "BlazorComponent", new ActionContext("BlazorError"), isHandled);
        EventPublisher.Publish(finalEventArgs);

        return base.OnErrorAsync(exception);
    }

    /// <summary>
    /// Recovers from the error state and re-renders the component.
    /// </summary>
    public new void Recover()
    {
        base.Recover();
    }
}
