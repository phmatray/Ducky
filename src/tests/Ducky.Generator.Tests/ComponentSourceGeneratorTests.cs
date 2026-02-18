using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace Ducky.Generator.Tests;

public class ComponentSourceGeneratorTests
{
    private const string StoreInterfaceSource =
        """
        namespace Ducky
        {
            public interface IStore<TRootState>
            {
                void Dispatch(object action);
                TRootState GetState();
                event System.Action<TRootState> StateChanged;
            }
        }
        """;

    private const string AppStateSource =
        """
        namespace TestApp
        {
            public class AppState
            {
                public CounterState Counter { get; set; }
                public TodosState Todos { get; set; }
            }
        }
        """;

    private const string CounterStateWithDefaultsSource =
        """
        namespace TestApp
        {
            [Ducky.Generators.DuckyComponent]
            public class CounterState
            {
                public int Count { get; set; }
            }
        }
        """;

    private const string ExpectedCounterComponentWithDefaults =
        """
        using System;
        using System.Collections.Generic;
        using Microsoft.AspNetCore.Components;
        using Ducky;
        
        namespace TestApp
        {
            public abstract class CounterStateComponent : ComponentBase, IDisposable
            {
                /// <summary>
                /// The injected store instance.
                /// </summary>
                [Inject]
                protected IStore<AppState> Store { get; set; } = default!;
        
                /// <summary>
                /// The current CounterState slice.
                /// </summary>
                protected CounterState State => Store.GetState().CounterState;
        
                /// <summary>
                /// Component initialization - subscribes to state changes.
                /// </summary>
                protected override void OnInitialized()
                {
                    Store.StateChanged += OnStateChanged;
                    base.OnInitialized();
                }
        
                /// <summary>
                /// Called whenever AppState changes; triggers UI refresh.
                /// </summary>
                private void OnStateChanged(AppState appState)
                {
                    InvokeAsync(StateHasChanged);
                }
        
                /// <summary>
                /// Dispatch any action.
                /// </summary>
                protected void Dispatch(object action)
                {
                    Store.Dispatch(action);
                }
        
                /// <summary>
                /// Unsubscribes from state changes when component is disposed.
                /// </summary>
                public void Dispose()
                {
                    Store.StateChanged -= OnStateChanged;
                }
            }
        }
        
        """;

    private const string TodosStateWithCustomPropertiesSource =
        """
        namespace TestApp
        {
            [Ducky.Generators.DuckyComponent(
                ComponentName = "TodosComponent",
                RootStateType = "AppState",
                StateSliceProperty = "Todos")]
            public class TodosState
            {
                public System.Collections.Generic.List<Todo> Items { get; set; }
                public bool IsLoading { get; set; }
            }
            
            public class Todo
            {
                public int Id { get; set; }
                public string Title { get; set; }
                public bool IsCompleted { get; set; }
            }
        }
        """;

    private const string ExpectedTodosComponent =
        """
        using System;
        using System.Collections.Generic;
        using Microsoft.AspNetCore.Components;
        using Ducky;
        
        namespace TestApp
        {
            public abstract class TodosComponent : ComponentBase, IDisposable
            {
                /// <summary>
                /// The injected store instance.
                /// </summary>
                [Inject]
                protected IStore<AppState> Store { get; set; } = default!;
        
                /// <summary>
                /// The current TodosState slice.
                /// </summary>
                protected TodosState State => Store.GetState().Todos;
        
                /// <summary>
                /// Component initialization - subscribes to state changes.
                /// </summary>
                protected override void OnInitialized()
                {
                    Store.StateChanged += OnStateChanged;
                    base.OnInitialized();
                }
        
                /// <summary>
                /// Called whenever AppState changes; triggers UI refresh.
                /// </summary>
                private void OnStateChanged(AppState appState)
                {
                    InvokeAsync(StateHasChanged);
                }
        
                /// <summary>
                /// Dispatch any action.
                /// </summary>
                protected void Dispatch(object action)
                {
                    Store.Dispatch(action);
                }
        
                /// <summary>
                /// Unsubscribes from state changes when component is disposed.
                /// </summary>
                public void Dispose()
                {
                    Store.StateChanged -= OnStateChanged;
                }
            }
        }
        
        """;

    private const string StateInDifferentNamespaceSource =
        """
        namespace AnotherNamespace
        {
            [Ducky.Generators.DuckyComponent(
                ComponentName = "SettingsBase",
                RootStateType = "TestApp.AppState",
                StateSliceProperty = "Settings")]
            public class SettingsState
            {
                public string Theme { get; set; }
                public bool NotificationsEnabled { get; set; }
            }
        }
        """;

    private const string ExpectedSettingsComponent =
        """
        using System;
        using System.Collections.Generic;
        using Microsoft.AspNetCore.Components;
        using Ducky;
        
        namespace AnotherNamespace
        {
            public abstract class SettingsBase : ComponentBase, IDisposable
            {
                /// <summary>
                /// The injected store instance.
                /// </summary>
                [Inject]
                protected IStore<TestApp.AppState> Store { get; set; } = default!;
        
                /// <summary>
                /// The current SettingsState slice.
                /// </summary>
                protected SettingsState State => Store.GetState().Settings;
        
                /// <summary>
                /// Component initialization - subscribes to state changes.
                /// </summary>
                protected override void OnInitialized()
                {
                    Store.StateChanged += OnStateChanged;
                    base.OnInitialized();
                }
        
                /// <summary>
                /// Called whenever TestApp.AppState changes; triggers UI refresh.
                /// </summary>
                private void OnStateChanged(TestApp.AppState appState)
                {
                    InvokeAsync(StateHasChanged);
                }
        
                /// <summary>
                /// Dispatch any action.
                /// </summary>
                protected void Dispatch(object action)
                {
                    Store.Dispatch(action);
                }
        
                /// <summary>
                /// Unsubscribes from state changes when component is disposed.
                /// </summary>
                public void Dispose()
                {
                    Store.StateChanged -= OnStateChanged;
                }
            }
        }
        
        """;

    [Fact]
    public void GeneratesComponentWithDefaultProperties()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        string[] sources = [StoreInterfaceSource, AppStateSource, CounterStateWithDefaultsSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        runResult.GeneratedTrees.ShouldNotBeEmpty();

        // Find the generated component file
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("CounterStateComponent.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString()
            .Replace("\r\n", "\n");  // Normalize line endings

        generatedText.ShouldBe(ExpectedCounterComponentWithDefaults);
    }

    [Fact]
    public void GeneratesComponentWithCustomProperties()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        string[] sources = [StoreInterfaceSource, AppStateSource, TodosStateWithCustomPropertiesSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        runResult.GeneratedTrees.ShouldNotBeEmpty();

        // Find the generated component file
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("TodosComponent.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString()
            .Replace("\r\n", "\n");  // Normalize line endings

        generatedText.ShouldBe(ExpectedTodosComponent);
    }

    [Fact]
    public void GeneratesComponentInDifferentNamespace()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        string[] sources = [StoreInterfaceSource, AppStateSource, StateInDifferentNamespaceSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        runResult.GeneratedTrees.ShouldNotBeEmpty();

        // Find the generated component file
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("SettingsBase.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString()
            .Replace("\r\n", "\n");  // Normalize line endings

        generatedText.ShouldBe(ExpectedSettingsComponent);
    }

    [Fact]
    public void GeneratesAttributeSource()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        string[] sources = [CounterStateWithDefaultsSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        runResult.GeneratedTrees.ShouldNotBeEmpty();

        // Find the generated attribute file
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("DuckyComponentAttribute.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        // Verify key parts of the attribute
        generatedText.ShouldContain("namespace Ducky.Generators");
        generatedText.ShouldContain("public class DuckyComponentAttribute : System.Attribute");
        generatedText.ShouldContain("public string? ComponentName { get; set; }");
        generatedText.ShouldContain("public string? RootStateType { get; set; }");
        generatedText.ShouldContain("public string? StateSliceProperty { get; set; }");
    }

    [Fact]
    public void IgnoresClassesWithoutAttribute()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        const string plainClassSource =
            """
            namespace TestApp
            {
                public class PlainClass
                {
                    public string Name { get; set; }
                }
            }
            """;

        string[] sources = [StoreInterfaceSource, plainClassSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        // Should only generate the attribute file, no component files
        runResult.GeneratedTrees.Length.ShouldBe(1);
        runResult.GeneratedTrees[0].FilePath.ShouldEndWith("DuckyComponentAttribute.g.cs");
    }

    [Fact]
    public void HandlesMultipleComponents()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        const string multipleStatesSource =
            """
            namespace TestApp
            {
                [Ducky.Generators.DuckyComponent]
                public class FirstState
                {
                    public int Value { get; set; }
                }
                
                [Ducky.Generators.DuckyComponent(ComponentName = "SecondComponent")]
                public class SecondState
                {
                    public string Name { get; set; }
                }
            }
            """;

        string[] sources = [StoreInterfaceSource, AppStateSource, multipleStatesSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        runResult.GeneratedTrees.Length.ShouldBe(3); // Attribute + 2 components

        List<SyntaxTree> componentFiles = runResult.GeneratedTrees
            .Where(tree => !tree.FilePath.EndsWith("DuckyComponentAttribute.g.cs"))
            .ToList();

        componentFiles.Count.ShouldBe(2);
        componentFiles.ShouldContain(tree => tree.FilePath.EndsWith("FirstStateComponent.g.cs"));
        componentFiles.ShouldContain(tree => tree.FilePath.EndsWith("SecondComponent.g.cs"));
    }

    [Fact]
    public void DoesNotSupportRecordTypes()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        const string recordStateSource =
            """
            namespace TestApp
            {
                [Ducky.Generators.DuckyComponent]
                public record RecordState(int Count, string Name);
            }
            """;

        string[] sources = [StoreInterfaceSource, AppStateSource, recordStateSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        // Should only generate the attribute file, no component for the record
        runResult.GeneratedTrees.Length.ShouldBe(1);
        runResult.GeneratedTrees[0].FilePath.ShouldEndWith("DuckyComponentAttribute.g.cs");
    }

    [Fact]
    public void GeneratesCorrectStatePropertyPath()
    {
        // Arrange
        ComponentSourceGenerator generator = new();

        const string nestedStateSource =
            """
            namespace TestApp
            {
                public class AppState
                {
                    public UIState UI { get; set; }
                }
                
                public class UIState
                {
                    public DialogState Dialog { get; set; }
                }
                
                [Ducky.Generators.DuckyComponent(
                    ComponentName = "DialogComponent",
                    RootStateType = "AppState",
                    StateSliceProperty = "UI.Dialog")]
                public class DialogState
                {
                    public bool IsOpen { get; set; }
                    public string Title { get; set; }
                }
            }
            """;

        string[] sources = [StoreInterfaceSource, nestedStateSource];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert
        SyntaxTree componentTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("DialogComponent.g.cs"));

        string generatedText = componentTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        // Verify the nested property path is used correctly
        generatedText.ShouldContain("protected DialogState State => Store.GetState().UI.Dialog;");
    }
}