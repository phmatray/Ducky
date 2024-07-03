using System.Collections.Concurrent;

namespace BzRx;

public static class ActionCreator
{
    public static readonly ConcurrentDictionary<string, int> RegisteredActionTypes = Globals.RegisteredActionTypes;

    public static IActionCreator CreateAction<T>(string type)
        where T : class
    {
        return CreateAction<T, object>(type, null);
    }

    public static IActionCreator CreateAction<T, P>(
        string type, ActionCreatorProps<P> config)
        where T : class where P : class
    {
        return CreateAction<T, Func<P, P>>(type, config);
    }

    public static IActionCreator CreateAction<T, P, R>(
        string type, Func<P, R> creator)
        where T : class
        where P : class
        where R : class
    {
        if (creator == null)
            throw new ArgumentNullException(nameof(creator));

        RegisteredActionTypes[type] = RegisteredActionTypes.ContainsKey(type) ? RegisteredActionTypes[type] + 1 : 1;

        return DefineType(type, creator);
    }

    public static IActionCreator CreateAction<T>(string type, object config = null) where T : class
    {
        RegisteredActionTypes[type] = RegisteredActionTypes.ContainsKey(type) ? RegisteredActionTypes[type] + 1 : 1;

        if (config is Func<object[], object> function)
        {
            return DefineType(type, new Func<object[], object>(args =>
            {
                var result = function(args);
                return new { result, type };
            }));
        }

        var asValue = config != null && config.GetType().GetProperty("_as")?.GetValue(config) as string;
        switch (asValue)
        {
            case null:
            case "empty":
                return DefineType(type, new Func<object>(() => new { type }));
            case "props":
                return DefineType(type, new Func<object, object>(props => { return new { props, type }; }));
            default:
                throw new ArgumentException("Unexpected config.");
        }
    }

    public static ActionCreatorProps<P> Props<P>()
        where P : class
    {
        return new ActionCreatorProps<P>
        {
            Props = default
        };
    }

    public static object? Union<TCreators>(TCreators creators)
        where TCreators : IDictionary<string, IActionCreator>
    {
        return null;
    }

    private static IActionCreator DefineType<T>(string type, Delegate creator)
        where T : class
    {
        var actionCreator = new ActionCreatorImpl<T>(type, creator);
        return actionCreator;
    }

    private class ActionCreatorImpl<T> : IActionCreator
        where T : class
    {
        public string Type { get; }
        public Delegate Creator { get; }

        public ActionCreatorImpl(string type, Delegate creator)
        {
            Type = type;
            Creator = creator;
        }
    }
}

public interface IActionCreator
{
    string Type { get; }
    Delegate Creator { get; }
}