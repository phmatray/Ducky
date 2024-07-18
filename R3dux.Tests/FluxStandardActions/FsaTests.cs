namespace R3dux.Tests.FluxStandardActions;

public class FsaTests
{
    [Fact]
    public void CreateTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        var title = "Learn Blazor";

        // Act
        var action = new TestCreateTodo(title);

        // Assert
        action.Payload.Title.Should().Be(title);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/create");
    }

    [Fact]
    public void ToggleTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = new TestToggleTodo(id);

        // Assert
        action.Payload.Id.Should().Be(id);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/toggle");
    }

    [Fact]
    public void DeleteTodo_ShouldInitializeCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = new TestDeleteTodo(id);

        // Assert
        action.Payload.Id.Should().Be(id);
        action.Meta.TimeStamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        action.TypeKey.Should().Be("todos/delete");
    }

    [Fact]
    public void FsaError_ShouldInitializeCorrectly()
    {
        // Arrange
        var exception = new Exception("An error occurred");

        // Act
        var action = new TestFsaError(exception);

        // Assert
        action.Payload.Should().Be(exception);
        action.Error.Should().BeTrue();
        action.TypeKey.Should().Be("error/action");
    }

    [Fact]
    public void FsaError_WithMeta_ShouldInitializeCorrectly()
    {
        // Arrange
        var exception = new Exception("An error occurred");
        var meta = new { Info = "Some metadata" };

        // Act
        var action = new TestFsaErrorWithMeta<object>(exception, meta);

        // Assert
        action.Payload.Should().Be(exception);
        action.Meta.Should().Be(meta);
        action.Error.Should().BeTrue();
        action.TypeKey.Should().Be("error/action");
    }
}