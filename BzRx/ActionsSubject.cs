using Microsoft.Extensions.DependencyInjection;
using R3;

namespace BzRx;

public static class Actions
{
    public const string INIT = "@bzrx/store/init";
}

public class ActionsSubject : ReactiveProperty<RxAction>
{
    public ActionsSubject()
        : base(new RxAction { Type = Actions.INIT }) { }

    public new void OnNext(RxAction action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action), "Actions must be objects");
        }

        if (action.Type == null)
        {
            throw new ArgumentException("Actions must have a type property", nameof(action));
        }

        base.OnNext(action);
    }
}

public static class ActionsSubjectProvider
{
    public static void AddActionsSubject(this IServiceCollection services)
    {
        services.AddSingleton<ActionsSubject>();
    }
}