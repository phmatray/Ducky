namespace BzRx;

public class FeatureConfig<FeatureName, FeatureState>
{
    public FeatureName Name { get; set; }
    public ActionReducer<FeatureState, IAction> Reducer { get; set; }
}

public class Feature<FeatureName, FeatureState> : FeatureConfig<FeatureName, FeatureState>
{
    public Dictionary<string, MemoizedSelector<Dictionary<string, object>, object>> Selectors { get; set; }
}

public delegate MemoizedSelector<Dictionary<string, object>, object> Selector(Dictionary<string, object> state);

public class MemoizedSelector<State, Result>
{
    public Func<State, Result> Projector { get; }

    public MemoizedSelector(Func<State, Result> projector)
    {
        Projector = projector;
    }
}

public class FeatureCreator
{
    public static Feature<FeatureName, FeatureState> CreateFeature<FeatureName, FeatureState>(
        FeatureConfig<FeatureName, FeatureState> featureConfig)
    {
        var featureSelector = CreateFeatureSelector<FeatureState>(featureConfig.Name.ToString());
        var nestedSelectors = CreateNestedSelectors(featureSelector, featureConfig.Reducer);
        var baseSelectors = new Dictionary<string, MemoizedSelector<Dictionary<string, object>, object>>
        {
            [$"select{Capitalize(featureConfig.Name.ToString())}State"] = featureSelector
        };
        foreach (var selector in nestedSelectors)
        {
            baseSelectors.Add(selector.Key, selector.Value);
        }

        return new Feature<FeatureName, FeatureState>
        {
            Name = featureConfig.Name,
            Reducer = featureConfig.Reducer,
            Selectors = baseSelectors
        };
    }

    private static MemoizedSelector<Dictionary<string, object>, FeatureState> CreateFeatureSelector<FeatureState>(
        string featureName)
    {
        return new MemoizedSelector<Dictionary<string, object>, FeatureState>(state =>
        {
            if (state.TryGetValue(featureName, out var featureState))
            {
                return (FeatureState)featureState;
            }

            return default;
        });
    }

    private static Dictionary<string, MemoizedSelector<Dictionary<string, object>, object>>
        CreateNestedSelectors<FeatureState>(
            MemoizedSelector<Dictionary<string, object>, FeatureState> featureSelector,
            ActionReducer<FeatureState, IAction> reducer)
    {
        var initialState = GetInitialState(reducer);
        var nestedKeys = initialState.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToList();

        return nestedKeys.ToDictionary(
            key => $"select{Capitalize(key)}",
            key => new MemoizedSelector<Dictionary<string, object>, object>(state =>
            {
                var featureState = featureSelector.Projector(state);
                return featureState.GetType().GetProperty(key)?.GetValue(featureState);
            }));
    }

    private static FeatureState GetInitialState<FeatureState>(ActionReducer<FeatureState, IAction> reducer)
    {
        return reducer(default, new InitAction());
    }

    private static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return char.ToUpper(text[0]) + text.Substring(1);
    }
}

public class InitAction : IAction
{
    public string Type => "@bzrx/feature/init";
}