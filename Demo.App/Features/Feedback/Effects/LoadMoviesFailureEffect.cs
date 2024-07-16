using Demo.AppStore;
using MudBlazor;
using R3;
using R3dux;

namespace Demo.App.Features.Feedback;

// ReSharper disable once UnusedType.Global
public class LoadMoviesFailureEffect(ISnackbar snackbar) : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState)
    {
        return actions
            .OfType<IAction, LoadMoviesFailure>()
            .Select(GetSnackBarMessage)
            .Do(message => snackbar.Add(message, Severity.Error))
            .Select(_ => (IAction)new SnackBarAction());
    }
    
    private static string GetSnackBarMessage(LoadMoviesFailure action)
    {
        Exception exception = action.Error;
        
        // return message and stack trace if available
        return exception.StackTrace is not null
            ? $"{exception.Message}\n{exception.StackTrace}"
            : exception.Message;
    }
}