// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Tests.Extensions.FluxStandardActions.Models;

namespace Ducky.Tests.Extensions.FluxStandardActions;

public class FsaTests
{
    [Fact]
    public void CreateTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        const string title = "Learn Blazor";

        // Act
        TestCreateTodo action = new(title);

        // Assert
        action.Payload.Title.Should().Be(title);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/create");
    }

    [Fact]
    public void ToggleTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        TestToggleTodo action = new(id);

        // Assert
        action.Payload.Id.Should().Be(id);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/toggle");
    }

    [Fact]
    public void DeleteTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        TestDeleteTodo action = new(id);

        // Assert
        action.Payload.Id.Should().Be(id);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/delete");
    }

    [Fact]
    public void FsaError_ShouldInitializeCorrectly()
    {
        // Arrange
        TestException exception = new();

        // Act
        TestFsaError action = new(exception);

        // Assert
        action.Payload.Should().Be(exception);
        FsaError.Error.Should().BeTrue();
        action.TypeKey.Should().Be("error/action");
    }

    [Fact]
    public void FsaError_WithMeta_ShouldInitializeCorrectly()
    {
        // Arrange
        TestException exception = new();
        var meta = new { Info = "Some metadata" };

        // Act
        TestFsaErrorWithMeta<object> action = new(exception, meta);

        // Assert
        action.Payload.Should().Be(exception);
        action.Meta.Should().Be(meta);
        FsaError.Error.Should().BeTrue();
        action.TypeKey.Should().Be("error/action");
    }
}
