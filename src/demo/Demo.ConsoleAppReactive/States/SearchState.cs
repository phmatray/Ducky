// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.ConsoleAppReactive.States;

// Search state for debounced effect demo
public record SearchState : IState
{
    public string Query { get; init; } = string.Empty;
    public int ResultCount { get; init; }
    public bool IsLoading { get; init; }
    public DateTime LastSearched { get; init; } = DateTime.UtcNow;
}

// Search actions
[DuckyAction]
public record SearchQuery(string Query);

[DuckyAction]
public record SearchStarted(string Query);

[DuckyAction]
public record SearchCompleted(string Query, int ResultCount);

// Search slice reducers
public record SearchSliceReducers : SliceReducers<SearchState>
{
    public override SearchState GetInitialState() => new();

    public SearchSliceReducers()
    {
        On<SearchStarted>((state, action) => state with
            {
                Query = action.Query,
                IsLoading = true
            });

        On<SearchCompleted>((state, action) => state with
            {
                Query = action.Query,
                ResultCount = action.ResultCount,
                IsLoading = false,
                LastSearched = DateTime.UtcNow
            });
    }
}
