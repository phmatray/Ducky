![Ducky banner](.github/banner.png)

# Ducky 🦆

A Predictable State Management Library for Blazor

| ![Logo MasterCommander](https://raw.githubusercontent.com/phmatray/Ducky/main/logo.png) | Ducky is a state management library designed for Blazor applications, inspired by the Redux pattern commonly used in JavaScript applications. It provides a predictable state container for .NET, ensuring that the application state is managed in a clear, consistent, and centralized manner. |
|-----------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

[![phmatray - Ducky](https://img.shields.io/static/v1?label=phmatray&message=Ducky&color=blue&logo=github)](https://github.com/phmatray/Ducky "Go to GitHub repo")
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![stars - Ducky](https://img.shields.io/github/stars/phmatray/Ducky?style=social)](https://github.com/phmatray/Ducky)
[![forks - Ducky](https://img.shields.io/github/forks/phmatray/Ducky?style=social)](https://github.com/phmatray/Ducky)

[![GitHub tag](https://img.shields.io/github/tag/phmatray/Ducky?include_prereleases=&sort=semver&color=blue)](https://github.com/phmatray/Ducky/releases/)
[![issues - Ducky](https://img.shields.io/github/issues/phmatray/Ducky)](https://github.com/phmatray/Ducky/issues)
[![GitHub pull requests](https://img.shields.io/github/issues-pr/phmatray/Ducky)](https://github.com/phmatray/Ducky/pulls)
[![GitHub contributors](https://img.shields.io/github/contributors/phmatray/Ducky)](https://github.com/phmatray/Ducky/graphs/contributors)
[![GitHub last commit](https://img.shields.io/github/last-commit/phmatray/Ducky)](https://github.com/phmatray/Ducky/commits/master)

---

## 📝 Table of Contents

<!-- TOC -->
* [Ducky 🦆](#ducky-)
  * [📝 Table of Contents](#-table-of-contents)
  * [📊 Stats](#-stats)
  * [📝 Summary](#-summary)
  * [🚀 How to Install](#-how-to-install)
  * [📌 Features](#-features)
    * [How Ducky Works](#how-ducky-works)
    * [The Counter Example](#the-counter-example)
  * [✨ Contributors](#-contributors)
  * [❓ Issues and Feature Requests](#-issues-and-feature-requests)
  * [✉️ Contact](#-contact)
  * [📜 License](#-license)
<!-- TOC -->

## 📊 Stats

![Alt](https://repobeats.axiom.co/api/embed/5c487f344d133cd5bb071285ba32b37a993cb6e0.svg "Repobeats analytics image")

## 📝 Summary

Ducky simplifies state management in Blazor applications by providing a structured and predictable way to handle state changes, inspired by the Redux pattern. It promotes best practices such as immutability, single source of truth, and clear separation of concerns, making it easier to manage complex application states.

## 🚀 How to Install

To install Ducky, you can use the following command:

```bash
dotnet add package Ducky
dotnet add package Ducky.Blazor
```

Alternatively, you can install the packages using the NuGet Package Manager in Visual Studio.


## 📌 Features

1. **Predictable State Management**: By following the principles of Redux, Ducky ensures that the application state is predictable. Every state change is described by an action and handled by a reducer, which returns a new state.
2. **Single Source of Truth**: The entire state of the application is stored in a single state tree, which makes debugging and state inspection easier.
3. **Immutability**: Ducky enforces immutability in state changes. Instead of mutating the existing state, reducers return a new state object, ensuring the integrity of the state over time.
4. **Actions and Reducers**: Actions describe the changes in the application, and reducers specify how the application's state changes in response to actions.
5. **Middleware and Effects**: Middleware allows for intercepting actions before they reach the reducer, enabling tasks such as logging, analytics, and asynchronous operations. Effects handle side effects like data fetching and other asynchronous tasks.
6. **Selectors**: Selectors are used to query the state in a performant manner. Memoized selectors help in reducing unnecessary recomputations, thus optimizing performance.
7. **Integration with Blazor**: Ducky is tailored for Blazor applications, integrating seamlessly with Blazor's component-based architecture.
8. **Source Generators**: Ducky uses source generators to generate code for dispatching actions, reducing boilerplate code and ensuring type safety.

### How Ducky Works

1. **State**: The application's state is represented by a single immutable object.
2. **Actions**: Actions are plain objects that describe what happened in the application.
3. **Reducers**: Reducers are pure functions that take the current state and an action, and return a new state.
4. **Dispatch**: The `dispatch` function sends an action to the store, which then forwards it to the reducer to compute the new state.
5. **Selectors**: Selectors are functions that select a piece of the state. Memoized selectors cache the results of state queries to improve performance.

### The Counter Example

Here is a simple example of how Ducky might be used in a Blazor application to manage a counter's state:

```csharp
namespace Demo.AppStore
{
    // State
    public record CounterState
    {
        public int Count { get; init; }
    }

    // Actions
    public record Increment(int Amount = 1);
    public record Decrement(int Amount = 1);
    public record Reset;

    // Reducers
    public record CounterReducers : SliceReducers<CounterState>
    {
        public CounterReducers()
        {
            On<Increment>((state, action) => state with { Count = state.Count + action.Amount });
            On<Decrement>((state, action) => state with { Count = state.Count - action.Amount });
            On<Reset>(_ => GetInitialState());
        }

        public override CounterState GetInitialState()
        {
            return new CounterState { Count = 0 };
        }
    }

    // Component
    @page "/counter"
    @inherits DuckyComponent<CounterState>

    <div>
        <p>Current count: @State.Count</p>
        <button @onclick="IncrementCount">Increment</button>
        <button @onclick="DecrementCount">Decrement</button>
        <button @onclick="ResetCount">Reset</button>
    </div>

    @code {
        private void IncrementCount() => Dispatch(new Increment());
        private void DecrementCount() => Dispatch(new Decrement());
        private void ResetCount() => Dispatch(new Reset());
    }
}
```

## ✨ Contributors

[![Contributors](https://contrib.rocks/image?repo=phmatray/Ducky)](http://contrib.rocks)

## ❓ Issues and Feature Requests

For reporting bugs or suggesting new features, kindly submit these as an issue to
the [Ducky Repository](https://github.com/phmatray/Ducky/issues). We value your contributions, but
before submitting an issue, please ensure it is not a duplicate of an existing one.

## ✉️ Contact

You can contact us by opening an issue on this repository.

<!-- portfolio-techstack:start -->

## Tech Stack

- **.NET 10 · .NET Standard 2.0**
- Nuke.Common
- System.Runtime.Serialization.Formatters
- Microsoft.AspNetCore.Components.WebAssembly
- Microsoft.AspNetCore.Components.WebAssembly.DevServer
- Humanizer.Core
- MudBlazor
- PublishSPAforGitHubPages.Build
- Microsoft.Extensions.DependencyInjection

<!-- portfolio-techstack:end -->

## 🗺️ Roadmap

- [ ] Redux DevTools-style time-travel debugging for Ducky stores
- [ ] Expand `Ducky.Generator` source generators to cut more action/reducer boilerplate
- [ ] Additional built-in middlewares (e.g. persistence, undo/redo)
- [ ] Broaden normalization and selector helpers for large, nested state trees
- [ ] More end-to-end samples beyond the counter demo (e.g. data-fetching, forms)

See the [open issues](https://github.com/phmatray/Ducky/issues) for details and to propose new ideas.

## 📜 License

Apache-2.0 License
