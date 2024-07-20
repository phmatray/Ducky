// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.Extensions.Normalization;

public class NormalizedStateTests
{
    [Fact]
    public void Indexer_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new SampleGuidEntity { Id = id, Name = "Test Entity" };
        var state = new SampleState().SetOne(entity);

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
    public void ContainsKey_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var state = new SampleState().SetOne(new SampleGuidEntity { Id = id, Name = "Test Entity" });

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
        var entity = new SampleGuidEntity { Id = id, Name = "Test Entity" };
        var state = new SampleState().SetOne(entity);

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
        var entity1 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var state = new SampleState().SetMany([entity1, entity2]);

        // Act
        var allIds = state.AllIds;

        // Assert
        allIds.Should().Contain([entity1.Id, entity2.Id]);
    }

    [Fact]
    public void SelectImmutableList_ShouldReturnAllEntities()
    {
        // Arrange
        var entity1 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var state = new SampleState().SetMany([entity1, entity2]);

        // Act
        var entities = state.SelectImmutableList();

        // Assert
        entities.Should().Contain([entity1, entity2]);
    }

    [Fact]
    public void SelectImmutableList_ShouldReturnEntitiesMatchingPredicate()
    {
        // Arrange
        var state = new SampleState().SetMany([
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" },
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" },
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Test Subject" }
        ]);

        // Act
        var result = state.SelectImmutableList(e => e.Name.Contains("Entity"));

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_ShouldMergeStates()
    {
        // Arrange
        var entity1 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var state1 = new SampleState().SetOne(entity1);
        var state2 = new SampleState().SetOne(entity2);

        // Act
        SampleState mergedState = state1.Merge(state2.ById);

        // Assert
        mergedState.ById.Should().ContainKeys(entity1.Id, entity2.Id);
        mergedState.ById[entity1.Id].Should().Be(entity1);
        mergedState.ById[entity2.Id].Should().Be(entity2);
    }

    [Fact]
    public void Merge_ShouldReplaceEntities_WhenStateContainsEntitiesWithSameKey()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new SampleGuidEntity { Id = id, Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = id, Name = "Entity 2" };
        var state1 = new SampleState().SetOne(entity1);
        var state2 = new SampleState().SetOne(entity2);

        // Act
        SampleState mergedState = state1.Merge(state2.ById, MergeStrategy.Overwrite);

        // Assert
        mergedState.ById.Should().ContainKey(id);
        mergedState.ById[id].Name.Should().Be("Entity 2");
    }

    [Fact]
    public void Merge_ShouldThrowException_WhenStateContainsEntitiesWithSameKey()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new SampleGuidEntity { Id = id, Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = id, Name = "Entity 2" };
        var state1 = new SampleState().SetOne(entity1);
        var state2 = new SampleState().SetOne(entity2);

        // Act
        Action act = () => state1.Merge(state2.ById);

        // Assert
        act.Should().Throw<R3duxException>().WithMessage($"Duplicate entity with key '{id}' found during merge.");
    }

    [Fact]
    public void Create_ShouldInitializeStateWithEntities()
    {
        // Arrange
        var entity1 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" };
        var entity2 = new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" };
        var entities = ImmutableList.Create(entity1, entity2);

        // Act
        var state = SampleState.Create(entities);

        // Assert
        state.ById.Should().ContainKeys(entity1.Id, entity2.Id);
        state.ById[entity1.Id].Should().Be(entity1);
        state.ById[entity2.Id].Should().Be(entity2);
    }

    [Fact]
    public void AddOne_ShouldAddEntity()
    {
        // Arrange
        var state = new SampleState();
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");

        // Act
        var newState = state.AddOne(entity);

        // Assert
        newState.ById.Should().ContainKey(entity.Id);
        newState[entity.Id].Should().Be(entity);
    }

    [Fact]
    public void AddMany_ShouldAddEntities()
    {
        // Arrange
        var state = new SampleState();
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };

        // Act
        var newState = state.AddMany(entities);

        // Assert
        newState.ById.Should().ContainKeys(entities.Select(e => e.Id));
    }

    [Fact]
    public void SetAll_ShouldReplaceEntities()
    {
        // Arrange
        var state = new SampleState();
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };

        // Act
        var newState = state.SetAll(entities);

        // Assert
        newState.ById.Should().Equal(entities.ToDictionary(e => e.Id));
    }

    [Fact]
    public void SetOne_ShouldReplaceEntity()
    {
        // Arrange
        var state = new SampleState();
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");

        // Act
        var newState = state.SetOne(entity);

        // Assert
        newState.ById.Should().ContainKey(entity.Id);
        newState[entity.Id].Should().Be(entity);
    }

    [Fact]
    public void SetMany_ShouldReplaceEntities()
    {
        // Arrange
        var state = new SampleState();
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };

        // Act
        var newState = state.SetMany(entities);

        // Assert
        newState.ById.Should().ContainKeys(entities.Select(e => e.Id));
    }

    [Fact]
    public void RemoveOne_ShouldRemoveEntity()
    {
        // Arrange
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        var state = new SampleState().AddOne(entity);

        // Act
        var newState = state.RemoveOne(entity.Id);

        // Assert
        newState.ById.Should().NotContainKey(entity.Id);
    }

    [Fact]
    public void RemoveMany_ShouldRemoveEntities()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState().AddMany(entities);

        // Act
        var newState = state.RemoveMany(entities.Select(e => e.Id));

        // Assert
        newState.ById.Should().NotContainKeys(entities.Select(e => e.Id));
    }

    [Fact]
    public void RemoveMany_ByPredicate_ShouldRemoveEntities()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState().AddMany(entities);

        // Act
        var newState = state.RemoveMany(e => e.Name.Contains("Entity"));

        // Assert
        newState.ById.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAll_ShouldClearEntities()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState().AddMany(entities);

        // Act
        var newState = state.RemoveAll();

        // Assert
        newState.ById.Should().BeEmpty();
    }

    [Fact]
    public void UpdateOne_ShouldUpdateEntity()
    {
        // Arrange
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        var state = new SampleState().AddOne(entity);

        // Act
        var newState = state.UpdateOne(entity.Id, e => e.Name = "Updated Entity");

        // Assert
        newState[entity.Id].Name.Should().Be("Updated Entity");
    }

    [Fact]
    public void UpdateMany_ShouldUpdateEntities()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState().AddMany(entities);

        // Act
        var newState = state.UpdateMany(entities.Select(e => e.Id), e => e.Name = "Updated Entity");

        // Assert
        newState.ById.Values.Should().AllSatisfy(e => e.Name.Should().Be("Updated Entity"));
    }

    [Fact]
    public void UpsertOne_ShouldAddOrUpdateEntity()
    {
        // Arrange
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        var state = new SampleState();

        // Act
        var newState = state.UpsertOne(entity);

        // Assert
        newState.ById.Should().ContainKey(entity.Id);
    }

    [Fact]
    public void UpsertMany_ShouldAddOrUpdateEntities()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState();

        // Act
        var newState = state.UpsertMany(entities);

        // Assert
        newState.ById.Should().ContainKeys(entities.Select(e => e.Id));
    }

    [Fact]
    public void MapOne_ShouldUpdateEntityUsingMapFunction()
    {
        // Arrange
        var entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        var state = new SampleState().AddOne(entity);

        // Act
        var newState = state.MapOne(entity.Id, e => new SampleGuidEntity(e.Id, "Mapped Entity"));

        // Assert
        newState[entity.Id].Name.Should().Be("Mapped Entity");
    }

    [Fact]
    public void Map_ShouldUpdateEntitiesUsingMapFunction()
    {
        // Arrange
        var entities = new List<SampleGuidEntity>
        {
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        };
        var state = new SampleState().AddMany(entities);

        // Act
        var newState = state.Map(e => new SampleGuidEntity(e.Id, "Mapped Entity"));

        // Assert
        newState.ById.Values.Should().AllSatisfy(e => e.Name.Should().Be("Mapped Entity"));
    }

    private static SampleGuidEntity CreateEntity(Guid id, string name)
    {
        return new SampleGuidEntity(id, name);
    }
}
