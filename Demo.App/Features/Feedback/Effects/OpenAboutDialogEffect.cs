using Demo.App.Components.Shared;
using MudBlazor;
using R3;
using R3dux;

namespace Demo.App.Features.Feedback;

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