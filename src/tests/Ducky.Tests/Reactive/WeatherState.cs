// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

// Weather state for polling effect demo
public record WeatherState : IState
{
    public string Location { get; init; } = "Unknown";
    public double Temperature { get; init; }
    public string Condition { get; init; } = "Unknown";
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    public bool IsLoading { get; init; }
    public string? Error { get; init; }
}

// Weather actions
public record StartWeatherPolling(string Location);

public record StopWeatherPolling;

public record WeatherLoading;

public record WeatherLoaded(string Location, double Temperature, string Condition);

public record WeatherError(string Message);

// Weather slice reducers
public record WeatherSliceReducers : SliceReducers<WeatherState>
{
    public override WeatherState GetInitialState() => new();

    public WeatherSliceReducers()
    {
        On<StartWeatherPolling>((state, action) => state with 
            { 
                Location = action.Location,
                IsLoading = false,
                Error = null 
            });

        On<WeatherLoading>(state => state with { IsLoading = true, Error = null });

        On<WeatherLoaded>((state, action) => state with
            {
                Location = action.Location,
                Temperature = action.Temperature,
                Condition = action.Condition,
                LastUpdated = DateTime.UtcNow,
                IsLoading = false,
                Error = null
            });

        On<WeatherError>((state, action) => state with
            {
                IsLoading = false,
                Error = action.Message
            });
    }
}
