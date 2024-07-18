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
            .Do(action => snackbar.Add(action.Error.Message, Severity.Error))
            .Select(action =>
            {
                var notification = new ExceptionNotification(action.Error);
                return (IAction)new AddNotification(notification);
            });
    }
}