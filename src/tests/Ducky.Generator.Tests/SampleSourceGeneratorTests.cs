using Ducky.Generator.Tests.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ducky.Generator.Tests;

public class SampleSourceGeneratorTests
{
    private const string DddRegistryText =
        """
        User
        Document
        Customer
        """;

    [Fact]
    public void GenerateClassesBasedOnDDDRegistry()
    {
        // Create an instance of the source generator.
        SampleSourceGenerator generator = new();

        // Source generators should be tested using 'GeneratorDriver'.
        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator],
            [
                // Add the additional file separately from the compilation.
                new TestAdditionalFile("./DDD.UbiquitousLanguageRegistry.txt", DddRegistryText)
            ]);

        // To run generators, we can use an empty compilation.
        CSharpCompilation compilation = CSharpCompilation.Create(nameof(SampleSourceGeneratorTests));
        // Run generators. Don't forget to use the new compilation rather than the previous one.
        GeneratorDriver x = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation newCompilation, out _);

        // Retrieve all files in the compilation.
        string[] generatedFiles = newCompilation.SyntaxTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToArray();

        // In this case, it is enough to check the file name.
        Assert.Equivalent(
            new[]
                {
                    "User.g.cs",
                    "Document.g.cs",
                    "Customer.g.cs"
                },
            generatedFiles);
    }
}
