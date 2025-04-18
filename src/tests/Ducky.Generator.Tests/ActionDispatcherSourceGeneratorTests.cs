using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Generator.Tests;

public class ActionDispatcherSourceGeneratorTests
{
    private const string DispatcherSource =
        """

        namespace Demo.BlazorWasm.AppStore
        {
            public interface IDispatcher
            {
                void Dispatch(object action);
            }
        }

        """;

    private const string FsaSource =
        """

        namespace Demo.BlazorWasm.AppStore
        {
            public abstract record Fsa<TPayload, TMeta>(TPayload Payload, TMeta Meta);
        }

        """;

    private const string IncrementActionSource =
        """

        namespace Demo.BlazorWasm.AppStore;

        [Ducky.Generators.DuckyAction]
        public record Increment(int Amount = 1);

        """;

    private const string ExpectedIncrementDispatcher =
        """
        // <auto-generated />
        // This file is auto-generated by Ducky.
        
        using System;

        public static partial class ActionDispatcher
        {
            /// <summary>
            /// Dispatches a new Increment action.
            /// </summary>
            /// <param name="dispatcher">The dispatcher instance.</param>
            public static void Increment(
                this Ducky.IDispatcher dispatcher,
                int amount = 1)
            {
                if (dispatcher is null)
                {
                    throw new System.ArgumentNullException(nameof(dispatcher));
                }
        
                dispatcher.Dispatch(new global::Demo.BlazorWasm.AppStore.Increment(amount));
            }
        }
        
        """;

    private const string CreateTodoActionSource =
        """

        namespace Demo.BlazorWasm.AppStore;

        [Ducky.Generators.DuckyAction]
        public sealed record CreateTodo : Fsa<CreateTodo.ActionPayload, CreateTodo.ActionMeta>
        {
            public CreateTodo(string title)
                : base(new ActionPayload(title), new ActionMeta(System.DateTime.UtcNow))
            {
            }
            public override string TypeKey => "todos/create";
        
            public sealed record ActionPayload(string Title);
        
            public sealed record ActionMeta(System.DateTime TimeStamp);
        }

        """;

    private const string ExpectedCreateTodoDispatcher =
        """
        // <auto-generated />
        // This file is auto-generated by Ducky.
        
        using System;

        public static partial class ActionDispatcher
        {
            /// <summary>
            /// Dispatches a new CreateTodo action.
            /// </summary>
            /// <param name="dispatcher">The dispatcher instance.</param>
            public static void CreateTodo(
                this Ducky.IDispatcher dispatcher,
                string title)
            {
                if (dispatcher is null)
                {
                    throw new System.ArgumentNullException(nameof(dispatcher));
                }
        
                dispatcher.Dispatch(new global::Demo.BlazorWasm.AppStore.CreateTodo(title));
            }
        }
        
        """;

    [Fact]
    public void GeneratesIncrementDispatchMethod()
    {
        // Arrange
        ActionDispatcherSourceGenerator generator = new();

        string[] sources = [DispatcherSource, IncrementActionSource];

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

        // Find the generated file for the Increment action.
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("ActionDispatcher.Increment.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        // Assert
        Assert.Equal(ExpectedIncrementDispatcher, generatedText, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void GeneratesCreateTodoDispatchMethod()
    {
        // Arrange
        ActionDispatcherSourceGenerator generator = new();

        string[] sources = [DispatcherSource, FsaSource, CreateTodoActionSource];

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

        // Find the generated file for the CreateTodo action.
        SyntaxTree generatedTree = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("ActionDispatcher.CreateTodo.g.cs"));

        string generatedText = generatedTree
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        // Assert
        Assert.Equal(ExpectedCreateTodoDispatcher, generatedText, ignoreLineEndingDifferences: true);
    }
}
