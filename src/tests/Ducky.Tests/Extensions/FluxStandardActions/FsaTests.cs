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
        action.Payload.Title.ShouldBe(title);
        action.Meta.TimeStamp.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.ShouldBe("todos/create");
    }

    [Fact]
    public void ToggleTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        TestToggleTodo action = new(id);

        // Assert
        action.Payload.Id.ShouldBe(id);
        action.Meta.TimeStamp.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.ShouldBe("todos/toggle");
    }

    [Fact]
    public void DeleteTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        TestDeleteTodo action = new(id);

        // Assert
        action.Payload.Id.ShouldBe(id);
        action.Meta.TimeStamp.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.ShouldBe("todos/delete");
    }

    [Fact]
    public void FsaError_ShouldInitializeCorrectly()
    {
        // Arrange
        TestException exception = new();

        // Act
        TestFsaError action = new(exception);

        // Assert
        action.Payload.ShouldBe(exception);
        FsaError.Error.ShouldBeTrue();
        action.TypeKey.ShouldBe("error/action");
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
        action.Payload.ShouldBe(exception);
        action.Meta.ShouldBe(meta);
        FsaError.Error.ShouldBeTrue();
        action.TypeKey.ShouldBe("error/action");
    }
}
