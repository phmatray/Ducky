// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Layout;

public sealed class LayoutReducersTests : IDisposable
{
    private const string Key = "demo-blazor-wasm-app-store-layout";

    private readonly LayoutReducers _sut = new();

    private readonly LayoutState _initialState = new()
    {
        Title = "Ducky",
        Version = DuckyVersioning.GetVersion().ToString(),
        IsDarkMode = true,
        IsDrawerOpen = true,
        IsNotificationOpen = false
    };

    private bool _disposed;

    [Fact]
    public void LayoutReducers_Should_Return_Initial_State()
    {
        // Act
        LayoutState initialState = _sut.GetInitialState();

        // Assert
        initialState.Title.ShouldBeEquivalentTo(_initialState.Title);
        initialState.Version.ShouldBeEquivalentTo(_initialState.Version);
        initialState.IsDarkMode.ShouldBe(_initialState.IsDarkMode);
        initialState.IsDrawerOpen.ShouldBe(_initialState.IsDrawerOpen);
        initialState.IsNotificationOpen.ShouldBe(_initialState.IsNotificationOpen);
    }

    [Fact]
    public void LayoutReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.ShouldBe(Key);
    }

    [Fact]
    public void LayoutReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.FullName.ShouldBe(typeof(LayoutState).FullName);
    }

    [Fact]
    public void LayoutReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<LayoutState, object, LayoutState>> reducers = _sut.Reducers;

        // Assert
        reducers.Count.ShouldBe(5); // 4 custom reducers + 1 hydration handler
    }

    [Fact]
    public void SetTitle_ShouldUpdateTitle()
    {
        // Arrange
        const string newTitle = "New Title";

        // Act
        LayoutState newState = _sut.Reduce(_initialState, new SetTitle(newTitle));

        // Assert
        newState.Title.ShouldBe(newTitle);
    }

    [Fact]
    public void SelectFullTitle_ShouldReturnCorrectFullTitle()
    {
        // Arrange
        Version version = DuckyVersioning.GetVersion();

        // Act
        string fullTitle = _initialState.SelectFullTitle();

        // Assert
        fullTitle.ShouldBe($"Ducky - {version}");
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
