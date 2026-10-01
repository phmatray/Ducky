using Gen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Gen.Tests;

public sealed class NamesGeneratorTests
{
    private const string Attribute = "public sealed class NamesAttribute : System.Attribute;";

    [Fact]
    public void Run_MarkedEnumInNamespace_EmitsLiveMembers()
    {
        var output = Run("namespace Shop; [Names] public enum Color { Red, [System.Obsolete] Green, Blue } public enum Plain { A }");

        output.ShouldBe([
            """
            namespace Shop;
            public static class ColorNames
            {
                public static string Of(Color value) => value switch
                {
                    Color.Red => "Red",
                    Color.Blue => "Blue",
                    _ => value.ToString(),
                };
            }

            """.ReplaceLineEndings(),
        ]);
    }

    [Fact]
    public void Run_MarkedEnumInGlobalNamespace_EmitsNoNamespace()
    {
        var output = Run("[Names] public enum Empty { }");

        output.ShouldHaveSingleItem().ShouldStartWith("public static class EmptyNames");
    }

    [Fact]
    public void Run_UnchangedInput_ReusesCachedOutput()
    {
        var compilation = Compile("[Names] public enum Size { S, M }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NamesGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver = driver.RunGenerators(compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Other;", cancellationToken: TestContext.Current.CancellationToken)), TestContext.Current.CancellationToken);

        var outputs = driver.GetRunResult().Results.Single().TrackedOutputSteps.SelectMany(s => s.Value).SelectMany(s => s.Outputs);
        outputs.ShouldAllBe(o => o.Reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void EnumInfo_Equality_ComparesMembers()
    {
        var info = new EnumInfo("E", null, ["A"]);

        info.Equals(new EnumInfo("E", null, ["A"])).ShouldBeTrue();
        info.Equals(new EnumInfo("E", null, ["B"])).ShouldBeFalse();
        info.Equals(new EnumInfo("F", null, ["A"])).ShouldBeFalse();
        info.Equals(new EnumInfo("E", "N", ["A"])).ShouldBeFalse();
        info.Equals(null).ShouldBeFalse();
        info.GetHashCode().ShouldBe(new EnumInfo("E", "N", ["B"]).GetHashCode());
    }

    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create(
        "Probe",
        [CSharpSyntaxTree.ParseText(Attribute), CSharpSyntaxTree.ParseText(source)],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static string[] Run(string source)
    {
        var driver = CSharpGeneratorDriver.Create(new NamesGenerator()).RunGenerators(Compile(source), TestContext.Current.CancellationToken);
        return [.. driver.GetRunResult().GeneratedTrees.Select(t => t.GetText(TestContext.Current.CancellationToken).ToString())];
    }
}
