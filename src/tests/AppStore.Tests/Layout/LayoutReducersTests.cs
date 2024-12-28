// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Layout;

public sealed class LayoutReducersTests : IDisposable
{
    private const string Key = "layout";

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
        initialState.Title.Should().BeEquivalentTo(_initialState.Title);
        initialState.Version.Should().BeEquivalentTo(_initialState.Version);
        initialState.IsDarkMode.Should().Be(_initialState.IsDarkMode);
        initialState.IsDrawerOpen.Should().Be(_initialState.IsDrawerOpen);
        initialState.IsNotificationOpen.Should().Be(_initialState.IsNotificationOpen);
    }

    [Fact]
    public void LayoutReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void LayoutReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<LayoutState>();
    }

    [Fact]
    public void LayoutReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<LayoutState, object, LayoutState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(4);
    }

    [Fact]
    public void SetTitle_ShouldUpdateTitle()
    {
        // Arrange
        const string newTitle = "New Title";

        // Act
        LayoutState newState = _sut.Reduce(_initialState, new SetTitle(newTitle));

        // Assert
        newState.Title.Should().Be(newTitle);
    }

    [Fact]
    public void SelectFullTitle_ShouldReturnCorrectFullTitle()
    {
        // Arrange
        Version version = DuckyVersioning.GetVersion();

        // Act
        string fullTitle = _initialState.SelectFullTitle();

        // Assert
        fullTitle.Should().Be($"Ducky - {version}");
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
