# Normalized State

Managing complex state structures efficiently is a common challenge in modern applications. R3dux addresses this challenge with **NormalizedState**—a powerful abstraction that simplifies the handling of collections of entities. By normalizing your state, you ensure that your application’s data is easy to manage, query, and update, all while maintaining the benefits of immutability.

## What is NormalizedState?

`NormalizedState` is a specialized record in R3dux designed to manage collections of entities in a normalized form. It organizes entities in an immutable dictionary keyed by a unique identifier, streamlining operations such as querying, updating, and deleting entities. This structure not only prevents data duplication but also makes relationships between entities more straightforward to handle.

### Key Characteristics

1. **Normalization**: Entities are stored in an `ImmutableDictionary`, indexed by a unique key (e.g., `Guid`, `int`). This eliminates redundancy and simplifies the management of related entities.
2. **Immutability**: All operations on the state return a new instance, ensuring that state changes are predictable, traceable, and free from side effects.
3. **Comprehensive API**: `NormalizedState` provides a rich set of methods for common operations, making it easier to manipulate collections of entities in a consistent and predictable way.

## Defining a Normalized State

To define a normalized state, extend the `NormalizedState<TKey, TEntity, TState>` record. This approach centralizes the management of your entities and provides built-in methods for common tasks.

### Example: Defining a Todo State

Consider a todo application where each todo item has a unique identifier. Here's how you would define a normalized state for managing todos:

```csharp
public record TodoState : NormalizedState<Guid, TodoItem, TodoState>
{
    // Selectors for retrieving specific subsets of todos

    public ImmutableList<TodoItem> SelectCompletedTodos()
    {
        return SelectImmutableList(todo => todo.IsCompleted);
    }

    public int SelectCompletedTodosCount()
    {
        return SelectCompletedTodos().Count;
    }

    public bool SelectHasCompletedTodos()
    {
        return !SelectCompletedTodos().IsEmpty;
    }

    public ImmutableList<TodoItem> SelectActiveTodos()
    {
        return SelectImmutableList(todo => !todo.IsCompleted);
    }

    public int SelectActiveTodosCount()
    {
        return SelectActiveTodos().Count();
    }

    public bool SelectHasActiveTodos()
    {
        return !SelectActiveTodos().IsEmpty;
    }
}
```

In this example:
- **TodoState** inherits from `NormalizedState<Guid, TodoItem, TodoState>`, managing a collection of `TodoItem` entities.
- Selectors are defined to encapsulate logic for querying the state, such as retrieving completed or active todos.

## Core Methods of NormalizedState

The `NormalizedState` record provides a comprehensive API for managing collections of entities. These methods ensure that all state changes are performed immutably and consistently.

### Entity Management Methods

Here’s an overview of the key methods available in `NormalizedState`:

- **Create**: Initializes a new state with a collection of entities.
  ```csharp
  public static TState Create(ImmutableList<TEntity> entities)
  ```
  Example:
  ```csharp
  var initialState = TodoState.Create(initialTodos);
  ```

- **AddOne**: Adds a single entity to the state.
  ```csharp
  public TState AddOne(TEntity entity)
  ```
  Example:
  ```csharp
  var newState = state.AddOne(new TodoItem(Guid.NewGuid(), "New Task"));
  ```

- **AddMany**: Adds multiple entities to the state.
  ```csharp
  public TState AddMany(IEnumerable<TEntity> entities)
  ```
  Example:
  ```csharp
  var newState = state.AddMany(newListOfTodos);
  ```

- **SetOne**: Replaces or adds a single entity.
  ```csharp
  public TState SetOne(TEntity entity)
  ```
  Example:
  ```csharp
  var updatedState = state.SetOne(updatedTodo);
  ```

- **SetMany**: Replaces or adds multiple entities.
  ```csharp
  public TState SetMany(IEnumerable<TEntity> entities)
  ```
  Example:
  ```csharp
  var updatedState = state.SetMany(updatedTodos);
  ```

- **RemoveOne**: Removes an entity by its key.
  ```csharp
  public TState RemoveOne(TKey key)
  ```
  Example:
  ```csharp
  var newState = state.RemoveOne(todoId);
  ```

- **RemoveMany**: Removes multiple entities by their keys.
  ```csharp
  public TState RemoveMany(IEnumerable<TKey> keys)
  ```
  Example:
  ```csharp
  var newState = state.RemoveMany(todoIds);
  ```

- **UpdateOne**: Updates a single entity using an action or function.
  ```csharp
  public TState UpdateOne(TKey key, Action<TEntity> update)
  public TState UpdateOne(TKey key, Func<TEntity, TEntity> update)
  ```
  Example:
  ```csharp
  var updatedState = state.UpdateOne(todoId, todo => todo.MarkAsCompleted());
  ```

- **UpdateMany**: Updates multiple entities using an action or function.
  ```csharp
  public TState UpdateMany(IEnumerable<TKey> keys, Action<TEntity> update)
  public TState UpdateMany(IEnumerable<TKey> keys, Func<TEntity, TEntity> update)
  ```
  Example:
  ```csharp
  var updatedState = state.UpdateMany(todoIds, todo => todo.MarkAsCompleted());
  ```

- **UpsertOne**: Updates or inserts a single entity.
  ```csharp
  public TState UpsertOne(TEntity entity)
  ```
  Example:
  ```csharp
  var newState = state.UpsertOne(newTodoItem);
  ```

- **UpsertMany**: Updates or inserts multiple entities.
  ```csharp
  public TState UpsertMany(IEnumerable<TEntity> entities)
  ```
  Example:
  ```csharp
  var newState = state.UpsertMany(todoItems);
  ```

- **MapOne**: Transforms a single entity by applying a function.
  ```csharp
  public TState MapOne(TKey key, Func<TEntity, TEntity> map)
  ```
  Example:
  ```csharp
  var mappedState = state.MapOne(todoId, todo => todo.WithPriority("High"));
  ```

- **Map**: Transforms all entities in the state.
  ```csharp
  public TState Map(Func<TEntity, TEntity> map)
  ```
  Example:
  ```csharp
  var mappedState = state.Map(todo => todo.WithCategory("Work"));
  ```

### Example: Adding, Updating, and Removing Entities

```csharp
// Adding a new todo item
var newState = state.AddOne(new TodoItem(Guid.NewGuid(), "New Task"));

// Updating an existing todo item
newState = newState.UpdateOne(todoId, todo => todo.MarkAsCompleted());

// Removing a todo item
newState = newState.RemoveOne(todoId);
```

## Advanced Operations with NormalizedState

### Merging Entities

When you need to merge a set of entities into the state, you can use the `Merge` method. This method allows for different merge strategies, such as overwriting existing entities or failing if duplicates are found.

```csharp
var mergedState = state.Merge(newEntitiesDictionary, MergeStrategy.Overwrite);
```

- **MergeStrategy.FailIfDuplicate**: Ensures that no duplicates are merged into the state.
- **MergeStrategy.Overwrite**: Overwrites any existing entities with the new ones.

### Querying Entities

Entities stored in a `NormalizedState` are indexed by their keys, making it easy to query them efficiently. You can retrieve an entity directly by its key using the indexer.

```csharp
var todo = state[todoId];
```

### Filtering with Selectors

Selectors allow you to define reusable queries that filter or transform the state. This keeps your components clean and focused on rendering UI rather than managing data.

```csharp
public ImmutableList<TodoItem> SelectCompletedTodos()
{
    return SelectImmutableList(todo => todo.IsCompleted);
}
```

## Understanding the Serialized State

When the state is serialized (for example, for debugging, logging, or persistence purposes), `NormalizedState` organizes your data in a structured way that highlights the benefits of normalization. Here’s how the state is typically represented in JSON:

```json
{
  "type": "AppStore.Todos.TodoState, AppStore, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
  "value": {
    "by-id": {
      "00000000-0000-0000-0000-111111111111": {
        "id": "00000000-0000-0000-0000-111111111111",
        "title": "Learn Blazor",
        "is-completed": true
      },
      "00000000-0000-0000-0000-222222222222": {
        "id": "00000000-0000-0000-0000-222222222222",
        "title": "Learn Redux",
        "is-completed": false
      },
      "00000000-0000-0000-0000-333333333333": {
        "id": "00000000-0000-0000-0000-333333333333",
        "title": "Learn Reactive Programming",
        "is-completed": false
      },
      "00000000-0000-0000-0000-444444444444": {
        "id": "00000000-0000-0000-0000-444444444444",
        "title": "Create a Todo App",
        "is-completed": true
      },
      "00000000-0000-0000-0000-555555555555": {
        "id": "00000000-0000-0000-0000-555555555555",
        "title": "Publish a NuGet package",
        "is-completed": false
      }
    },
    "all-ids": [
      "00000000-0000-0000-0000-111111111111",
      "00000000-0000-0000-0000-222222222222",
      "00000000-0000-0000-0000-333333333333",
      "00000000-0000-0000-0000-444444444444",
      "00000000-0000-0000-0000-555555555555"
    ]
  }
}
```

#### Breakdown of the Serialized Structure

1. **`type`**: This field indicates the fully qualified name of the state type, including the assembly information. It tells the system how to deserialize this state back into its original type.
2. **`by-id`**: This dictionary holds the entities indexed by their unique keys. Each key corresponds to an entity's ID, and the value is the entity itself, represented as a JSON object.
3. **`all-ids`**: This array lists all the keys (IDs) of the entities stored in the state. It preserves the order of the entities as they were added, which can be useful for displaying items in a particular order.

#### Advantages of the Serialized Format

- **Efficiency**: By storing entities in a dictionary, operations like lookups, updates, and deletions are very efficient.
- **Clarity**: The separation between the `by-id` dictionary and the `all-ids` list provides a clear distinction between the entities themselves and their ordering, making it easier to understand and manipulate the state.
- **Consistency**: When rehydrating (deserializing) the state, the structure ensures that all entities are restored accurately, maintaining the relationships and order as intended.

This structured approach not only makes the state more manageable within your application but also ensures that any serialized data is robust, easy to inspect, and straightforward to restore.

## Best Practices for Using NormalizedState

- **Embrace Immutability**: Always use the provided methods to modify the state. This ensures that all changes are made immutably, which is crucial for maintaining predictable state transitions.
- **Utilize Selectors**: Encapsulate querying logic within selectors. This not only simplifies your components but also promotes reuse across your application.
- **Carefully Choose Merge Strategies**: When merging entities, choose the appropriate strategy to avoid unintentional overwrites or errors due to duplicates.
- **Encapsulate Common Operations**: Define common operations (such as adding, updating, or removing entities) within your state class. This promotes consistency and reduces the risk of errors.
