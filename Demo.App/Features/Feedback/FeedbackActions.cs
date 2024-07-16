using R3dux;

namespace Demo.App.Features.Feedback;

/// <summary>
/// Action dispatched after a successful or failed snackbar message.
/// </summary>
public record SnackBarAction : IAction;

/// <summary>
/// Action to open the About dialog.
/// </summary>
public record OpenAboutDialog : IAction;

/// <summary>
/// Action that does nothing.
/// </summary>
public record NoOp : IAction;