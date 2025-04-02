# Selectors

Selectors in Ducky are functions that allow you to extract and compute derived data from the global state. They encapsulate the logic for retrieving specific pieces of data and can be used to optimize performance by minimizing unnecessary recalculations.

## What are Selectors?

Selectors are specialized functions designed to query and derive specific data from the state. They help you avoid duplicating logic across components and can improve performance by memoizing computed values, ensuring that they are only recalculated when necessary.

### Key Characteristics of Selectors

1. **Encapsulation**: Selectors encapsulate the logic for selecting data from the state, making your components simpler and more focused.
2. **Reusability**: Since selectors are functions, they can be reused across different parts of your application.
3. **Memoization**: Selectors can be memoized to prevent unnecessary recalculations, which can significantly improve performance in large applications.

## Basic Selectors

Selectors can be defined as methods within your state classes. These methods are responsible for querying the state and returning the relevant data.

### Example: Basic Selectors

Here’s an example of a `MoviesState` record with basic selectors:

```C#
public record MoviesState
{
    public required ImmutableDictionary<int, Movie> Movies { get; init; }

    public required bool IsLoading { get; init; }

    public required string? ErrorMessage { get; init; }

    public required Pagination Pagination { get; init; }

    // Selectors
    // ==========
    // Selectors are defined as methods that encapsulate the logic of selecting data from the state.
    // Each method should begin with the word "Select".
    
    public int SelectMovieCount()
    {
        return Movies.Count;
    }

    public Movie? SelectMovieById(int id)
    {
        return Movies.GetValueOrDefault(id);
    }

    public ImmutableDictionary<int, Movie> SelectMoviesByYear()
    {
        return Movies
            .OrderByDescending(pair => pair.Value.Year)
            .ToImmutableDictionary();
    }
}
```

In this example:
- The `SelectMovieCount` selector returns the total number of movies.
- The `SelectMovieById` selector retrieves a movie by its ID.
- The `SelectMoviesByYear` selector returns the movies sorted by their release year in descending order.

## Advanced Selectors

As your application grows, you might need more advanced selectors that can handle complex queries or derived data. Ducky allows you to create **memoized selectors** that cache their results and recalculate only when the relevant parts of the state have changed.

### Example: Advanced Selectors with Memoization

Here’s an example of a `ProductState` record that uses memoized selectors:

```C#
public record ProductState : NormalizedState<Guid, Product, ProductState>
{
    private readonly Func<ProductState, ImmutableList<Product>> _selectElectronics;
    private readonly Func<ProductState, ImmutableList<Product>> _selectClothing;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfElectronics;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfClothing;

    public ProductState()
    {
        _selectElectronics = MemoizedSelector.Create<ProductState, ImmutableList<Product>>(
            state => state.SelectImmutableList(product => product.Category == "Electronics"),
            state => state.ById);

        _selectClothing = MemoizedSelector.Create<ProductState, ImmutableList<Product>>(
            state => state.SelectImmutableList(product => product.Category == "Clothing"),
            state => state.ById);

        _selectTotalPriceOfElectronics = MemoizedSelector.Compose(
            _selectElectronics,
            products => products.Sum(product => product.Price),
            state => state.ById);

        _selectTotalPriceOfClothing = MemoizedSelector.Compose(
            _selectClothing,
            products => products.Sum(product => product.Price),
            state => state.ById);
    }

    // Memoized Selectors
    public ImmutableList<Product> SelectElectronics()
    {
        return _selectElectronics(this);
    }

    public ImmutableList<Product> SelectClothing()
    {
        return _selectClothing(this);
    }

    public decimal SelectTotalPriceOfElectronics()
    {
        return _selectTotalPriceOfElectronics(this);
    }

    public decimal SelectTotalPriceOfClothing()
    {
        return _selectTotalPriceOfClothing(this);
    }
}
```

In this example:
- **Memoized Selectors**: The selectors are memoized using the `MemoizedSelector.Create` and `MemoizedSelector.Compose` methods to avoid unnecessary recomputation.
- **Advanced Queries**: The `SelectElectronics` and `SelectClothing` selectors return lists of products filtered by category.
- **Derived Data**: The `SelectTotalPriceOfElectronics` and `SelectTotalPriceOfClothing` selectors compute the total price of products in each category, leveraging memoization to enhance performance.

## Using Selectors in Blazor Components

A best practice when using selectors in Blazor components is to create properties that use the selectors, keeping the component's markup separate from its logic. This approach makes your components more maintainable and easier to read.

### Example: Using Selectors in a Blazor Component

Here’s an example of a Blazor component that uses selectors to manage a Todo list:

```html
@page "/todo"
@inherits DuckyComponent<TodoState>

<PageTitle>Ducky - Todo List</PageTitle>

<MudGrid>
  <MudItem xs="12">
    <MudStack>
      <MudText Typo="Typo.subtitle1">Example</MudText>
      <MudText Typo="Typo.h3">Todo List</MudText>
      <MudText Typo="Typo.body1">
        Manage your todos effectively with Ducky, featuring functionality to add, toggle, and remove todo items.
      </MudText>
    </MudStack>
  </MudItem>

  <MudItem xs="12" lg="6">
    <MudStack>
      <MudBadge Content="@ActiveTodosCount" Color="Color.Primary">
        <MudText Typo="Typo.h5">Active Todos</MudText>
      </MudBadge>
      
      <MudPaper>
        @if (!HasActiveTodos)
        {
          <MudText Typo="Typo.h6" Class="ma-4">
            No active todos - add a new todo item to get started!
          </MudText>
        }
        <MudList T="TodoItem">
          @foreach (var todo in ActiveTodos)
          {
            <MudListItem @key="todo.Id">
              <MudStack Row Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                <MudIconButton Icon="@Icons.Material.Filled.CheckBoxOutlineBlank" OnClick="() => ToggleTodoItem(todo.Id)"/>
                <MudText>@todo.Title</MudText>
                <MudSpacer/>
                <MudButton Variant="Variant.Outlined" Color="Color.Error" OnClick="() => DeleteTodoItem(todo.Id)">Delete</MudButton>
              </MudStack>
            </MudListItem>
          }
        </MudList>
      </MudPaper>

      <MudCard>
        <MudCardContent>
          <MudTextField @bind-Value="_newTodo" Variant="Variant.Filled" Label="New Todo" />
        </MudCardContent>
        <MudCardActions>
          <MudButton Variant="Variant.Outlined" OnClick="CreateTodoItem">Add Todo</MudButton>
        </MudCardActions>
      </MudCard>
    </MudStack>
  </MudItem>

  <MudItem xs="12" lg="6">
    <MudStack>
      <MudBadge Content="@CompletedTodosCount" Color="Color.Secondary">
        <MudText Typo="Typo.h5">Completed Todos</MudText>
      </MudBadge>
      <MudPaper>
        <MudList T="TodoItem">
          @foreach (var todo in CompletedTodos)
          {
            <MudListItem @key="todo.Id">
              <MudStack Row Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                <MudIconButton Icon="@Icons.Material.Filled.CheckBox" OnClick="() => ToggleTodoItem(todo.Id)"/>
                <MudText Style="text-decoration: line-through;">@todo.Title</MudText>
                <MudSpacer/>
                <MudButton Variant="Variant.Outlined" Color="Color.Error" OnClick="() => DeleteTodoItem(todo.Id)">Delete</MudButton>
              </MudStack>
            </MudListItem>
          }
        </MudList>
      </MudPaper>
    </MudStack>
  </MudItem>
</MudGrid>

@code {

  private string _newTodo = string.Empty;

  // Using Selectors as Properties
  private ImmutableList<TodoItem> ActiveTodos => State.SelectActiveTodos();
  private int ActiveTodosCount => State.SelectActiveTodosCount();
  private bool HasActiveTodos => State.SelectActiveTodosCount() > 0;
  
  private ImmutableList<TodoItem> CompletedTodos => State.SelectCompletedTodos();
  private int CompletedTodosCount => State.SelectCompletedTodosCount();

  private void CreateTodoItem()
  {
      if (!string.IsNullOrWhiteSpace(_newTodo))
      {
          Dispatch(new CreateTodoItem(_newTodo));
          _newTodo = string.Empty;
      }
  }

  private void ToggleTodoItem(Guid id)
  {
      Dispatch(new ToggleTodoItem(id));
  }

  private void DeleteTodoItem(Guid id)
  {
      Dispatch(new DeleteTodoItem(id));
  }
}
```

In this example:
- **Selectors as Properties**: The component defines properties like `ActiveTodos`, `ActiveTodosCount`, and `HasActiveTodos` that use selectors to access state. This keeps the markup clean and the logic encapsulated.
- **Clean Markup**: The component’s markup is clean and focused on UI elements, with the business logic separated into the `@code` section.
- **Reusability**: The selector-based properties can be reused in other components, promoting consistency and reducing code duplication.

## Best Practices for Using Selectors in Blazor

- **Encapsulate Logic**: Use selectors within your state classes to encapsulate data retrieval logic, keeping your components focused on rendering UI.
- **Separate Markup and Logic**: Define properties in your components that use selectors. This approach keeps your Razor markup clean and easy to maintain.
- **Leverage Memoization**: For expensive computations or derived data, use memoized selectors to optimize performance.
- **Reusability**: Reuse selectors across your application to maintain consistency and avoid duplicating logic.
