using Demo.Website2.Components.Shared;
using R3;

namespace Demo.Website2.Features.Feedback;

// ReSharper disable once UnusedType.Global
public class OpenAboutDialogEffect(IDialogService dialog) : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<RootState> rootState)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        
        return actions
            .OfType<IAction, OpenAboutDialog>()
            .Do(_ => dialog.ShowAsync<AboutDialog>(null, options))
            .Select(_ => (IAction)new NoOp());
    }
}
