namespace Ducky.CodeGen.Core;

public class ProfilingGenerator : SourceGeneratorBase<ProfilingGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(ProfilingGeneratorOptions opts)
    {
        return new()
        {
            Usings = new List<string> { "System" },
            Namespaces = new List<NamespaceElement> { new() { Name = "MyNameSpace" } }
        };
    }
}
