// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Extensions.Normalization;

public class NormalizedStateTests
{
    [Fact]
    public void Indexer_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SampleGuidEntity entity = new() { Id = id, Name = "Test Entity" };
        SampleGuidState state = new SampleGuidState().SetOne(entity);

        // Act
        SampleGuidEntity result = state[id];

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenEntityDoesNotExist()
    {
        // Arrange
        SampleGuidState state = new();

        // Act
        Action act = () => _ = state[Guid.NewGuid()];

        // Assert
        act.ShouldThrow<DuckyException>("The entity does not exist.");
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SampleGuidState state = new SampleGuidState().SetOne(new SampleGuidEntity { Id = id, Name = "Test Entity" });

        // Act
        bool containsKey = state.ContainsKey(id);

        // Assert
        containsKey.ShouldBeTrue();
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Arrange
        SampleGuidState state = new();

        // Act
        bool containsKey = state.ContainsKey(Guid.NewGuid());

        // Assert
        containsKey.ShouldBeFalse();
    }

    [Fact]
    public void ContainsKey_ShouldThrowException_WhenKeyIsEmpty()
    {
        // Arrange
        SampleStringState state = new();

        // Act
        Action act = () => state.ContainsKey(string.Empty);

        // Assert
        act.ShouldThrow<DuckyException>("The key cannot be empty.");
    }

    [Fact]
    public void GetByKey_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SampleGuidEntity entity = new() { Id = id, Name = "Test Entity" };
        SampleGuidState state = new SampleGuidState().SetOne(entity);

        // Act
        SampleGuidEntity result = state.GetByKey(id);

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void GetByKey_ShouldThrowException_WhenEntityDoesNotExist()
    {
        // Arrange
        SampleGuidState state = new();

        // Act
        Action act = () => state.GetByKey(Guid.NewGuid());

        // Assert
        act.ShouldThrow<DuckyException>("The entity does not exist.");
    }

    [Fact]
    public void AllIds_ShouldReturnAllKeys()
    {
        // Arrange
        SampleGuidEntity entity1 = new() { Id = Guid.NewGuid(), Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = Guid.NewGuid(), Name = "Entity 2" };
        SampleGuidState state = new SampleGuidState().SetMany([entity1, entity2]);

        // Act
        ValueCollection<Guid> allIds = state.AllIds;

        // Assert
        allIds.ShouldContain(entity1.Id);
        allIds.ShouldContain(entity2.Id);
    }

    [Fact]
    public void SelectEntities_ShouldReturnAllEntities()
    {
        // Arrange
        SampleGuidEntity entity1 = new() { Id = Guid.NewGuid(), Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = Guid.NewGuid(), Name = "Entity 2" };
        SampleGuidState state = new SampleGuidState().SetMany([entity1, entity2]);

        // Act
        ValueCollection<SampleGuidEntity> entities = state.SelectEntities();

        // Assert
        entities.ShouldContain(entity1);
        entities.ShouldContain(entity2);
    }

    [Fact]
    public void SelectEntities_ShouldReturnEntitiesMatchingPredicate()
    {
        // Arrange
        SampleGuidState state = new SampleGuidState().SetMany([
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 1" },
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Entity 2" },
            new SampleGuidEntity { Id = Guid.NewGuid(), Name = "Test Subject" }
        ]);

        // Act
        ValueCollection<SampleGuidEntity> result = state.SelectEntities(e => e.Name.Contains("Entity"));

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Merge_ShouldMergeStates()
    {
        // Arrange
        SampleGuidEntity entity1 = new() { Id = Guid.NewGuid(), Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = Guid.NewGuid(), Name = "Entity 2" };
        SampleGuidState state1 = new SampleGuidState().SetOne(entity1);
        SampleGuidState state2 = new SampleGuidState().SetOne(entity2);

        // Act
        SampleGuidState mergedState = state1.Merge(state2.ById);

        // Assert
        mergedState.ById.ShouldContainKey(entity1.Id);
        mergedState.ById[entity1.Id].ShouldBe(entity1);

        mergedState.ById.ShouldContainKey(entity2.Id);
        mergedState.ById[entity2.Id].ShouldBe(entity2);
    }

    [Fact]
    public void Merge_ShouldReplaceEntities_WhenStateContainsEntitiesWithSameKey()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SampleGuidEntity entity1 = new() { Id = id, Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = id, Name = "Entity 2" };
        SampleGuidState state1 = new SampleGuidState().SetOne(entity1);
        SampleGuidState state2 = new SampleGuidState().SetOne(entity2);

        // Act
        SampleGuidState mergedState = state1.Merge(state2.ById, MergeStrategy.Overwrite);

        // Assert
        mergedState.ById.ShouldContainKey(id);
        mergedState.ById[id].Name.ShouldBe("Entity 2");
    }

    [Fact]
    public void Merge_ShouldThrowException_WhenStateContainsEntitiesWithSameKey()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SampleGuidEntity entity1 = new() { Id = id, Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = id, Name = "Entity 2" };
        SampleGuidState state1 = new SampleGuidState().SetOne(entity1);
        SampleGuidState state2 = new SampleGuidState().SetOne(entity2);

        // Act
        Action act = () => state1.Merge(state2.ById);

        // Assert
        act.ShouldThrow<DuckyException>($"Duplicate entity with key '{id}' found during merge.");
    }

    [Fact]
    public void Create_ShouldInitializeStateWithEntities()
    {
        // Arrange
        SampleGuidEntity entity1 = new() { Id = Guid.NewGuid(), Name = "Entity 1" };
        SampleGuidEntity entity2 = new() { Id = Guid.NewGuid(), Name = "Entity 2" };
        ValueCollection<SampleGuidEntity> entities = [entity1, entity2];

        // Act
        SampleGuidState state = SampleGuidState.Create(entities);

        // Assert
        state.ById.ShouldContainKey(entity1.Id);
        state.ById[entity1.Id].ShouldBe(entity1);

        state.ById.ShouldContainKey(entity2.Id);
        state.ById[entity2.Id].ShouldBe(entity2);
    }

    [Fact]
    public void AddOne_ShouldAddEntity()
    {
        // Arrange
        SampleGuidState state = new();
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");

        // Act
        SampleGuidState newState = state.AddOne(entity);

        // Assert
        newState.ById.ShouldContainKey(entity.Id);
        newState[entity.Id].ShouldBe(entity);
    }

    [Fact]
    public void AddMany_ShouldAddEntities()
    {
        // Arrange
        SampleGuidState state = new();
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];

        // Act
        SampleGuidState newState = state.AddMany(entities);

        // Assert
        // newState.ById.ShouldContainKeys(entities.Select(e => e.Id));
        // ==> shouldly
        newState.ById.ShouldAllBe(pair => entities.Select(e => e.Id).Contains(pair.Key));
    }

    [Fact]
    public void SetAll_ShouldReplaceEntities()
    {
        // Arrange
        SampleGuidState state = new();
        List<SampleGuidEntity> entities = 
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];

        // Act
        SampleGuidState newState = state.SetAll(entities);

        // Assert
        newState.ById.ShouldBeEquivalentTo(entities.ToImmutableDictionary(e => e.Id));
    }

    [Fact]
    public void SetOne_ShouldReplaceEntity()
    {
        // Arrange
        SampleGuidState state = new();
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");

        // Act
        SampleGuidState newState = state.SetOne(entity);

        // Assert
        newState.ById.ShouldContainKey(entity.Id);
        newState[entity.Id].ShouldBe(entity);
    }

    [Fact]
    public void SetMany_ShouldReplaceEntities()
    {
        // Arrange
        SampleGuidState state = new();
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];

        // Act
        SampleGuidState newState = state.SetMany(entities);

        // Assert
        newState.ById.ShouldAllBe(pair => entities.Select(e => e.Id).Contains(pair.Key));
    }

    [Fact]
    public void RemoveOne_ShouldRemoveEntity()
    {
        // Arrange
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        SampleGuidState state = new SampleGuidState().AddOne(entity);

        // Act
        SampleGuidState newState = state.RemoveOne(entity.Id);

        // Assert
        newState.ById.ShouldNotContainKey(entity.Id);
    }

    [Fact]
    public void RemoveOne_WithEmptyStringKey_ShouldThrowException()
    {
        // Arrange
        SampleStringState state = new();

        // Act
        Action act = () => state.RemoveOne(string.Empty);

        // Assert
        act.ShouldThrow<DuckyException>("The key cannot be empty.");
    }

    [Fact]
    public void RemoveMany_ShouldRemoveEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.RemoveMany(entities.Select(e => e.Id));

        // Assert
        newState.ById.ShouldAllBe(pair => entities.Select(e => e.Id).Contains(pair.Key));
    }

    [Fact]
    public void RemoveMany_ByPredicate_ShouldRemoveEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.RemoveMany(e => e.Name.Contains("Entity"));

        // Assert
        newState.ById.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveAll_ShouldClearEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.RemoveAll();

        // Assert
        newState.ById.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateOne_ShouldUpdateEntity()
    {
        // Arrange
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        SampleGuidState state = new SampleGuidState().AddOne(entity);

        // Act
        SampleGuidState newState = state.UpdateOne(entity.Id, e => e.Name = "Updated Entity");

        // Assert
        newState[entity.Id].Name.ShouldBe("Updated Entity");
    }

    [Fact]
    public void UpdateOne_WithFunc_ShouldUpdateEntity()
    {
        // Arrange
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        SampleGuidState state = new SampleGuidState().AddOne(entity);

        // Act
        SampleGuidState newState = state.UpdateOne(entity.Id, e => new SampleGuidEntity(e.Id, "Updated Entity"));

        // Assert
        newState[entity.Id].Name.ShouldBe("Updated Entity");
    }

    [Fact]
    public void UpdateMany_ShouldUpdateEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.UpdateMany(entities.Select(e => e.Id), e => e.Name = "Updated Entity");

        // Assert
        newState.ById.Values.ShouldAllBe(e => e.Name == "Updated Entity");
    }

    [Fact]
    public void UpdateMany_WithFunc_ShouldUpdateEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.UpdateMany(entities.Select(e => e.Id), e => new SampleGuidEntity(e.Id, "Updated Entity"));

        // Assert
        newState.ById.Values.ShouldAllBe(e => e.Name == "Updated Entity");
    }

    [Fact]
    public void UpsertOne_ShouldAddOrUpdateEntity()
    {
        // Arrange
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        SampleGuidState state = new();

        // Act
        SampleGuidState newState = state.UpsertOne(entity);

        // Assert
        newState.ById.ShouldContainKey(entity.Id);
    }

    [Fact]
    public void UpsertMany_ShouldAddOrUpdateEntities()
    {
        // Arrange
        List<SampleGuidEntity> entities = 
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new();

        // Act
        SampleGuidState newState = state.UpsertMany(entities);

        // Assert
        newState.ById.ShouldAllBe(pair => entities.Select(e => e.Id).Contains(pair.Key));
    }

    [Fact]
    public void MapOne_ShouldUpdateEntityUsingMapFunction()
    {
        // Arrange
        SampleGuidEntity entity = CreateEntity(Guid.NewGuid(), "Test Entity");
        SampleGuidState state = new SampleGuidState().AddOne(entity);

        // Act
        SampleGuidState newState = state.MapOne(entity.Id, e => new SampleGuidEntity(e.Id, "Mapped Entity"));

        // Assert
        newState[entity.Id].Name.ShouldBe("Mapped Entity");
    }

    [Fact]
    public void Map_ShouldUpdateEntitiesUsingMapFunction()
    {
        // Arrange
        List<SampleGuidEntity> entities =
        [
            CreateEntity(Guid.NewGuid(), "Entity 1"),
            CreateEntity(Guid.NewGuid(), "Entity 2")
        ];
        SampleGuidState state = new SampleGuidState().AddMany(entities);

        // Act
        SampleGuidState newState = state.Map(e => new SampleGuidEntity(e.Id, "Mapped Entity"));

        // Assert
        newState.ById.Values.ShouldAllBe(e => e.Name == "Mapped Entity");
    }

    private static SampleGuidEntity CreateEntity(in Guid id, string name)
    {
        return new(id, name);
    }
}
