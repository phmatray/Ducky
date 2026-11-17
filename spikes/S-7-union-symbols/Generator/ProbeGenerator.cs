using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Spike;

[Generator]
public sealed class ProbeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(context.CompilationProvider, static (ctx, compilation) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Roslyn host: {typeof(Compilation).Assembly.GetName().Version}; LanguageVersion: {((Microsoft.CodeAnalysis.CSharp.CSharpCompilation)compilation).LanguageVersion}");
            Describe(sb, "source", compilation.Assembly.GlobalNamespace, "CartAction");
            foreach (var r in compilation.References.Select(compilation.GetAssemblyOrModuleSymbol).OfType<IAssemblySymbol>())
            {
                if (r.Name == "Lib") Describe(sb, "referenced (" + r.Name + ")", r.GlobalNamespace, "RefAction");
            }
            ctx.AddSource("Probe.g.cs", "namespace Spike; internal static class Probe { public const string Report = \"\"\"\n" + sb + "\"\"\"; }");
        });

    private static void Describe(StringBuilder sb, string where, INamespaceSymbol root, string name)
    {
        var t = Find(root, name);
        if (t is null) { sb.AppendLine($"[{where}] {name}: NOT FOUND"); return; }
        sb.AppendLine($"[{where}] {t.ToDisplayString()}: Kind={t.Kind} TypeKind={t.TypeKind} IsValueType={t.IsValueType} IsReferenceType={t.IsReferenceType} IsRecord={t.IsRecord} IsSealed={t.IsSealed}");
        sb.AppendLine($"  BaseType={t.BaseType?.ToDisplayString()}  Interfaces=[{string.Join(", ", t.AllInterfaces.Select(i => i.ToDisplayString()))}]");
        sb.AppendLine($"  Attributes=[{string.Join(", ", t.GetAttributes().Select(a => a.AttributeClass?.ToDisplayString()))}]");
        foreach (var c in t.Constructors) sb.AppendLine($"  ctor: {c.ToDisplayString()} decl={c.DeclaredAccessibility} implicit={c.IsImplicitlyDeclared}");
        foreach (var m in t.GetMembers().Where(m => m is not IMethodSymbol { MethodKind: MethodKind.Constructor }))
            sb.AppendLine($"  member: {m.Kind} {m.ToDisplayString()} decl={m.DeclaredAccessibility} implicit={m.IsImplicitlyDeclared}");
    }

    private static INamedTypeSymbol? Find(INamespaceSymbol ns, string name)
    {
        foreach (var t in ns.GetTypeMembers(name)) return t;
        foreach (var child in ns.GetNamespaceMembers())
            if (Find(child, name) is { } f) return f;
        return null;
    }
}
