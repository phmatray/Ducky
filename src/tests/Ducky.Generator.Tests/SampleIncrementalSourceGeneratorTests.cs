using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ducky.Generator.Tests;

public class SampleIncrementalSourceGeneratorTests
{
    private const string VectorClassText =
        """

        namespace TestNamespace;

        [Ducky.Generators.Report]
        public partial class Vector3
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }
        """;

    private const string ExpectedGeneratedClassText =
        """
        // <auto-generated/>

        using System;
        using System.Collections.Generic;

        namespace TestNamespace;

        partial class Vector3
        {
            public IEnumerable<string> Report()
            {
                yield return $"X:{this.X}";
                yield return $"Y:{this.Y}";
                yield return $"Z:{this.Z}";
            }
        }

        """;

    [Fact]
    public void GenerateReportMethod()
    {
        // Create an instance of the source generator.
        SampleIncrementalSourceGenerator generator = new();

        // Source generators should be tested using 'GeneratorDriver'.
        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        CSharpCompilation compilation = CSharpCompilation.Create(
            nameof(SampleSourceGeneratorTests),
            [CSharpSyntaxTree.ParseText(VectorClassText)],
            [
                // To support 'System.Attribute' inheritance, add reference to 'System.Private.CoreLib'.
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        // Run generators and retrieve all results.
        GeneratorDriverRunResult runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        SyntaxTree generatedFileSyntax = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Vector3.g.cs"));

        // Complex generators should be tested using text comparison.
        Assert.Equal(
            ExpectedGeneratedClassText,
            generatedFileSyntax.GetText().ToString(),
            ignoreLineEndingDifferences: true);
    }
}
