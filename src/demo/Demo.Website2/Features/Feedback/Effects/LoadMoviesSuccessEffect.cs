using R3;

namespace Demo.Website2.Features.Feedback;

// ReSharper disable once UnusedType.Global
public class LoadMoviesSuccessEffect(ISnackbar snackbar) : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<RootState> rootState)
    {
        return actions
            .OfType<IAction, LoadMoviesSuccess>()
            .Select(GetSnackBarMessage)
            .Do(message => snackbar.Add(message, Severity.Success))
            .Select(message =>
            {
                var notification = new SuccessNotification(message);
                return (IAction)new AddNotification(notification);
            });
    }
    
    private static string GetSnackBarMessage(LoadMoviesSuccess action)
        => $"Loaded {action.Movies.Count} movies from the server.";
    
    
}