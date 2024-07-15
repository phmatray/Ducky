using Demo.AppStore;
using MudBlazor;
using R3;
using R3dux;

namespace Demo.App.Effects;

// ReSharper disable once UnusedType.Global
public class LoadMoviesSuccessEffect(ISnackbar snackbar) : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState)
    {
        return actions
            .OfType<IAction, LoadMoviesSuccess>()
            .Select(GetSnackBarMessage)
            .Do(message => snackbar.Add(message, Severity.Success))
            .Select(_ => (IAction)new SnackBarAction());
    }
    
    private static string GetSnackBarMessage(LoadMoviesSuccess action)
        => $"Loaded {action.Movies.Length} movies from the server.";
}
