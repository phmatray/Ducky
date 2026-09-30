using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Ducky.TestSupport;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(ActionTypeAttribute).Assembly).ShouldBeEmpty();

    [Fact]
    public void NoStaticMutableFields_DetectsPlantedMutableStatic()
    {
        var fixture = CompileFixture("""
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using Microsoft.CodeAnalysis;

            namespace Fixture;

            public static class Planted
            {
                public static int Counter;
                public static readonly List<int> Cache = [];
                public static List<int> Held { get; } = [];

                public static readonly DiagnosticDescriptor Rule =
                    new("FIX001", "Title", "Message", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);
                public static readonly string Name = "fixture";
                public static readonly object Unset = new();
                public static readonly Func<int> One = () => 1;
                public static readonly ImmutableArray<int> Items = [1, 2];
                public const int Answer = 42;

                public static Func<int, int> Double() => x => x * 2;
            }
            """);

        // The exempted lambda cache (a non-readonly static of <>c) must exist, or "not reported" proves nothing.
        fixture.GetTypes()
            .Where(t => t.IsDefined(typeof(CompilerGeneratedAttribute)))
            .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .ShouldContain(f => !f.IsInitOnly);
        StaticFieldAudit.Violations(fixture).ShouldBe(
            ["Fixture.Planted.Counter", "Fixture.Planted.Cache", "Fixture.Planted.<Held>k__BackingField"],
            ignoreOrder: true);
    }

    private static Assembly CompileFixture(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Fixture",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        stream.Position = 0;
        return new AssemblyLoadContext("Fixture", isCollectible: true).LoadFromStream(stream);
    }
}
