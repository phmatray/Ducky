namespace BzRx;

public delegate TResult ProjectorFn<out TResult>(params object[] args);
public delegate bool ComparatorFn(object a, object b);

public class MemoizedProjection
{
    public Func<object[], object> Memoized { get; set; }
    public Action Reset { get; set; }
    public Action<object> SetResult { get; set; }
    public Action ClearResult { get; set; }
}

public static class Selectors
{
    public static bool IsEqualCheck(object a, object b)
        => a.Equals(b);

    public static bool IsArgumentsChanged(object[] args, object[] lastArguments, ComparatorFn comparator)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!comparator(args[i], lastArguments[i]))
            {
                return true;
            }
        }
        return false;
    }

    public static MemoizedProjection ResultMemoize(
        Func<object[], object> projectionFn,
        ComparatorFn isResultEqual)
    {
        return DefaultMemoize(projectionFn, IsEqualCheck, isResultEqual);
    }

    public static MemoizedProjection DefaultMemoize(
        Func<object[], object> projectionFn,
        ComparatorFn? isArgumentsEqual = null,
        ComparatorFn? isResultEqual = null)
    {
        isArgumentsEqual ??= IsEqualCheck;
        isResultEqual ??= IsEqualCheck;

        object[]? lastArguments = null;
        object? lastResult = null;
        object? overrideResult = null;

        void Reset()
        {
            lastArguments = null;
            lastResult = null;
        }

        void SetResult(object? result = null)
        {
            overrideResult = result;
        }

        void ClearResult()
        {
            overrideResult = null;
        }

        object Memoized(params object[] args)
        {
            if (overrideResult != null)
            {
                return overrideResult;
            }

            if (lastArguments == null)
            {
                lastResult = projectionFn(args);
                lastArguments = args;
                return lastResult;
            }

            if (!IsArgumentsChanged(args, lastArguments, isArgumentsEqual))
            {
                return lastResult;
            }

            var newResult = projectionFn(args);
            lastArguments = args;

            if (isResultEqual(lastResult, newResult))
            {
                return lastResult;
            }

            lastResult = newResult;
            return newResult;
        }

        return new MemoizedProjection
        {
            Memoized = Memoized,
            Reset = Reset,
            SetResult = SetResult,
            ClearResult = ClearResult
        };
    }

    public static MemoizedSelector<State, Result> CreateSelector<State, S1, Result>(
        Selector<State, S1> s1,
        Func<S1, Result> projector)
    {
        return CreateSelectorFactory(DefaultMemoize)(s1, projector);
    }

    public static MemoizedSelector<State, Result> CreateSelector<State, S1, S2, Result>(
        Selector<State, S1> s1,
        Selector<State, S2> s2,
        Func<S1, S2, Result> projector)
    {
        return CreateSelectorFactory(DefaultMemoize)(s1, s2, projector);
    }

    // Add more overloads for CreateSelector as needed

    public static MemoizedSelector<State, Result> CreateSelector<State, Props, S1, Result>(
        SelectorWithProps<State, Props, S1> s1,
        Func<S1, Props, Result> projector)
    {
        return (MemoizedSelector<State, Result>)CreateSelectorFactory(DefaultMemoize, new SelectorFactoryConfig<State, Result> { StateFn = DefaultStateFn })(s1, projector);
    }

    // Add more overloads for CreateSelector with Props as needed

    public static MemoizedSelector<State, Result> CreateSelectorFactory<State, Result>(
        Func<Func<object[], object>, MemoizedProjection> memoize,
        SelectorFactoryConfig<State, Result>? options = null)
    {
        options ??= new SelectorFactoryConfig<State, Result> { StateFn = DefaultStateFn };

        return new Func<object[], MemoizedSelector<State, Result>>(input =>
        {
            var args = input.ToList();

            var selectors = args.Take(args.Count - 1).ToList();
            var projector = (Func<object[], object>)args.Last();
            var memoizedSelectors = selectors.OfType<MemoizedSelector<State, object>>().ToList();

            var memoizedProjector = memoize(args => projector(args));

            var memoizedState = DefaultMemoize((object[] stateAndProps) =>
            {
                var state = stateAndProps[0];
                var props = stateAndProps.Length > 1 ? stateAndProps[1] : null;
                return options.StateFn(state, selectors.Cast<Selector<State, object>>().ToList(), props, memoizedProjector);
            });

            void Release()
            {
                memoizedState.Reset();
                memoizedProjector.Reset();
                memoizedSelectors.ForEach(selector => selector.Release());
            }

            return new MemoizedSelector<State, Result>
            {
                Memoized = memoizedState.Memoized,
                Projector = projector,
                SetResult = memoizedState.SetResult,
                ClearResult = memoizedState.ClearResult,
                Release = Release
            };
        });
    }

    public static object DefaultStateFn<State>(State state, List<Selector<State, object>> selectors, object? props, MemoizedProjection memoizedProjector)
    {
        if (props == null)
        {
            var args = selectors.Select(selector => selector(state)).ToArray<object>();
            return memoizedProjector.Memoized(args);
        }

        var argsWithProps = selectors.Select(selector => selector(state)).Concat<object>(new[] { props }).ToArray();
        return memoizedProjector.Memoized(argsWithProps);
    }

    public static MemoizedSelector<object, T> CreateFeatureSelector<T>(string featureName)
    {
        return CreateSelector<object, T>(
            state =>
            {
                var featureState = state.GetType().GetProperty(featureName)?.GetValue(state);
                if (featureState == null && IsDevMode() && !IsNgrxMockEnvironment())
                {
                    Console.WriteLine($"@ngrx/store: The feature name \"{featureName}\" does not exist in the state, therefore createFeatureSelector cannot access it. Be sure it is imported in a loaded module using StoreModule.forRoot('{featureName}', ...) or StoreModule.forFeature('{featureName}', ...). If the default state is intended to be undefined, as is the case with router state, this development-only warning message can be ignored.");
                }
                return (T)featureState;
            },
            featureState => featureState);
    }

    private static bool IsNgrxMockEnvironment()
    {
        // Implement your logic to check if it's a mock environment
        return false;
    }

    private static bool IsDevMode()
    {
        // Implement your logic to check if it's in development mode
        return true;
    }

    private static bool IsSelectorsDictionary(object selectors)
    {
        return selectors is Dictionary<string, Delegate>;
    }

    private static object[] ExtractArgsFromSelectorsDictionary(
        Dictionary<string, Delegate> selectorsDictionary)
    {
        var selectors = selectorsDictionary.Values.Cast<Selector<object, object>>().ToList();
        var resultKeys = selectorsDictionary.Keys.ToList();
        Func<object[], object> projector = selectorResults =>
        {
            var result = new Dictionary<string, object>();
            for (var index = 0; index < resultKeys.Count; index++)
            {
                result[resultKeys[index]] = selectorResults[index];
            }
            return result;
        };

        return selectors.Append<object>((object)projector).ToArray();
    }
}

public class SelectorFactoryConfig<T, V>
{
    public Func<T, List<Selector<T, object>>, object, MemoizedProjection, V> StateFn { get; set; }
}

public class MemoizedSelector<TState, TResult>
{
    public Func<object[], TResult> Memoized { get; set; }
    public ProjectorFn<TResult> Projector { get; set; }
    public Action<object> SetResult { get; set; }
    public Action ClearResult { get; set; }
    public Action Release { get; set; }
}

public class MemoizedSelectorWithProps<TState, TProps, TResult>
    : MemoizedSelector<TState, TResult>
{
}

