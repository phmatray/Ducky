using static BzRx.Helpers;

namespace BzRx;

public static class ActionGroupCreator
{
    public static Dictionary<string, IActionCreator> CreateActionGroup<Source, Events>(
        ActionGroupConfig<Source, Events> config)
        where Source : class
        where Events : Dictionary<string, object>
    {
        var source = config.Source;
        var events = config.Events;

        return events.Keys.Aggregate(
            new Dictionary<string, IActionCreator>(),
            (actionGroup, eventName) =>
            {
                var actionType = ToActionType(source, eventName);
                var actionName = ToActionName(eventName);

                actionGroup[actionName] = ActionCreator.CreateAction(actionType, events[eventName]);

                return actionGroup;
            });
    }

    public static ActionCreatorProps<void> EmptyProps()
    {
        return ActionCreator.Props<void>();
    }

    private static string ToActionName(string eventName)
    {
        var words = eventName.Trim().Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = i == 0 ? Uncapitalize(words[i]) : Capitalize(words[i]);
        }
        return string.Join(string.Empty, words);
    }

    private static string ToActionType<Source>(Source source, string eventName)
    {
        return $"[{source}] {eventName}";
    }
}

public class ActionGroupConfig<TSource, TEvents>
{
    public TSource Source { get; set; }
    public TEvents Events { get; set; }
}