using System.Collections.Immutable;
using R3dux.Exceptions;
using R3dux.Normalization;

namespace R3dux.Tests.Normalization;

public class NormalizedStateTests
{
    [Fact]
    public void Indexer_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new SampleEntity { Id = id, Name = "Test Entity" };
        var state = new SampleState().AddOrUpdate(entity);

        // Act
        var result = state[id];

        // Assert
        result.Should().Be(entity);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenEntityDoesNotExist()
    {
        // Arrange
        var state = new SampleState();

        // Act
        Action act = () => _ = state[Guid.NewGuid()];

        // Assert
        act.Should().Throw<R3duxException>().WithMessage("The entity does not exist.");
    }
    
    [Fact]
    public void AddOrUpdate_ShouldAddEntity()
    {
        // Arrange
        var state = new SampleState();
        var entity = new SampleEntity { Id = Guid.NewGuid(), Name = "Test Entity" };

        // Act
        var newState = state.AddOrUpdate(entity);

        // Assert
        newState.ById.Should().ContainKey(entity.Id);
        newState.ById[entity.Id].Should().Be(entity);
    }

    [Fact]
    public void AddOrUpdate_ShouldUpdateEntity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var state = new SampleState().AddOrUpdate(new SampleEntity { Id = id, Name = "Old Name" });
        var updatedEntity = new SampleEntity { Id = id, Name = "Updated Name" };

        // Act
        var newState = state.AddOrUpdate(updatedEntity);

        // Assert
        newState.ById[id].Name.Should().Be("Updated Name");
    }

    [Fact]
    public void Remove_ShouldRemoveEntity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var state = new SampleState().AddOrUpdate(new SampleEntity { Id = id, Name = "Test Entity" });

        // Act
        var newState = state.Remove(id);

        // Assert
        newState.ById.Should().NotContainKey(id);
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var state = new SampleState().AddOrUpdate(new SampleEntity { Id = id, Name = "Test Entity" });

        // Act
        var containsKey = state.ContainsKey(id);

        // Assert
        containsKey.Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Arrange
        var state = new SampleState();

        // Act
        var containsKey = state.ContainsKey(Guid.NewGuid());

        // Assert
        containsKey.Should().BeFalse();
    }

    [Fact]
    public void GetByKey_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new SampleEntity { Id = id, Name = "Test Entity" };
        var state = new SampleState().AddOrUpdate(entity);

        // Act
        var result = state.GetByKey(id);

        // Assert
        result.Should().Be(entity);
    }

    [Fact]
    public void GetByKey_ShouldThrowException_WhenEntityDoesNotExist()
    {
        // Arrange
        var state = new SampleState();

        // Act
        Action act = () => state.GetByKey(Guid.NewGuid());

        // Assert
        act.Should().Throw<R3duxException>().WithMessage("The entity does not exist.");
    }

    [Fact]
    public void AllIds_ShouldReturnAllKeys()
    {
        // Arrange
        var entity1 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var state = new SampleState().AddOrUpdate(entity1).AddOrUpdate(entity2);

        // Act
        var allIds = state.AllIds;

        // Assert
        allIds.Should().Contain(new[] { entity1.Id, entity2.Id });
    }

    [Fact]
    public void SelectImmutableList_ShouldReturnAllEntities()
    {
        // Arrange
        var entity1 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var state = new SampleState().AddOrUpdate(entity1).AddOrUpdate(entity2);

        // Act
        var entities = state.SelectImmutableList();

        // Assert
        entities.Should().Contain(new[] { entity1, entity2 });
    }

    [Fact]
    public void SelectImmutableList_ShouldReturnEntitiesMatchingPredicate()
    {
        // Arrange
        var state = new SampleState()
            .AddOrUpdate(new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 1" })
            .AddOrUpdate(new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 2" })
            .AddOrUpdate(new SampleEntity { Id = Guid.NewGuid(), Name = "Test Subject" });

        // Act
        var result = state.SelectImmutableList(e => e.Name.Contains("Entity"));

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Create_ShouldInitializeStateWithEntities()
    {
        // Arrange
        var entity1 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var entities = ImmutableList.Create(entity1, entity2);

        // Act
        var state = SampleState.Create(entities);

        // Assert
        state.ById.Should().ContainKeys(entity1.Id, entity2.Id);
        state.ById[entity1.Id].Should().Be(entity1);
        state.ById[entity2.Id].Should().Be(entity2);
    }
}

/// <summary>
/// Represents a sample entity with an identifier.
/// </summary>
file record SampleEntity : IEntity<Guid>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Represents a sample normalized state for collections.
/// </summary>
file record SampleState : NormalizedState<Guid, SampleEntity, SampleState>
{
    // No additional implementation needed for the tests
}