#:package Microsoft.CodeAnalysis.CSharp
#:package CsCheck
#:property TreatWarningsAsErrors=false
#:property RestorePackagesWithLockFile=false
#:property PublishAot=false
// The documentation IDs of BannedSymbols.txt, test/BannedSymbols.txt and test/BannedSymbols.covered.txt.
// BannedApiAnalyzers has no wildcards and ignores an entry that resolves to no symbol, so a renamed or re-shaped
// overload (an SDK, ASP.NET or CsCheck bump) silently unbans it. Versions come from Directory.Packages.props.
//   dotnet run test/banned-ids.cs            prints each generated group; paste new IDs into the lists
//   dotnet run test/banned-ids.cs -- check   exits 1 if a list entry resolves to no symbol or a generated ID is missing
//                                            from its list (a bump added an overload)
// ponytail: run by hand until M0-03 lands; M0-03 adds `dotnet run test/banned-ids.cs -- check` to ExclusionGate.
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../.."));
var major = Environment.Version.Major;
string RefPack(string name) =>
    Directory.GetDirectories(Path.Combine(root, "packs", name), $"{major}.*").OrderBy(d => Version.Parse(Path.GetFileName(d).Split('-')[0])).Last()
    + $"/ref/net{major}.0";
string[] packs = [RefPack("Microsoft.NETCore.App.Ref"), RefPack("Microsoft.AspNetCore.App.Ref")];
var refs = packs
    .SelectMany(d => Directory.GetFiles(d, "*.dll"))
    .Append(typeof(CsCheck.Check).Assembly.Location)
    .Select(p => MetadataReference.CreateFromFile(p));
var c = CSharpCompilation.Create("x", references: refs);

INamedTypeSymbol T(string n) => c.GetTypeByMetadataName(n) ?? throw new InvalidOperationException(n);
IEnumerable<IMethodSymbol> M(string t) => T(t).GetMembers().OfType<IMethodSymbol>()
    .Where(m => m.DeclaredAccessibility == Accessibility.Public && m.MethodKind is MethodKind.Ordinary or MethodKind.Constructor);
// Every generated ID, with its group and the list that must ban it.
var gen = new List<(string Group, string List, string Id)>();
void P(string h, string list, IEnumerable<IMethodSymbol> ms) =>
    // Roslyn appends "~ReturnType" to some IDs; the lists omit it (no conversion operator is listed, so nothing is ambiguous).
    gen.AddRange(ms.Select(m => DocumentationCommentId.CreateDeclarationId(m)!.Split('~')[0]).Distinct().Order(StringComparer.Ordinal).Select(id => (h, list, id)));

// BannedSymbols.txt (src, SPEC §10)
P("json", "BannedSymbols.txt", M("System.Text.Json.JsonSerializer").Where(m => !m.Parameters.Any(p => p.Type.Name == "JsonTypeInfo")));
P("gettype", "BannedSymbols.txt", M("System.Type").Where(m => m.Name == "GetType" && m.IsStatic));
P("load", "BannedSymbols.txt", M("System.Reflection.Assembly").Where(m => m.Name.StartsWith("Load", StringComparison.Ordinal) && m.IsStatic));
P("activator", "BannedSymbols.txt", M("System.Activator").Where(m => m.Name == "CreateInstance" && m.Parameters.Length > 0 && m.Parameters[0].Type.Name == "Type"));
foreach (var t in (string[])["Microsoft.JSInterop.IJSRuntime", "Microsoft.JSInterop.IJSObjectReference", "Microsoft.JSInterop.IJSInProcessRuntime", "Microsoft.JSInterop.IJSInProcessObjectReference"])
{
    P(t, "BannedSymbols.txt", M(t));
}

// test/BannedSymbols.covered.txt (SPEC §17.1): every CsCheck runner with a threads parameter (Sample* included).
P("cscheck", "test/BannedSymbols.covered.txt", M("CsCheck.Check").Where(m => m.Parameters.Any(p => p.Name == "threads")));
P("taskrun", "test/BannedSymbols.covered.txt", M("System.Threading.Tasks.Task").Where(m => m.Name == "Run"));
P("taskstart", "test/BannedSymbols.covered.txt", M("System.Threading.Tasks.Task").Where(m => m.Name == "Start"));
P("startnew", "test/BannedSymbols.covered.txt", M("System.Threading.Tasks.TaskFactory").Where(m => m.Name == "StartNew"));
P("startnew1", "test/BannedSymbols.covered.txt", M("System.Threading.Tasks.TaskFactory`1").Where(m => m.Name == "StartNew"));
P("threadctor", "test/BannedSymbols.covered.txt", M("System.Threading.Thread").Where(m => m.MethodKind == MethodKind.Constructor));
P("timerctor", "test/BannedSymbols.covered.txt", M("System.Threading.Timer").Where(m => m.MethodKind == MethodKind.Constructor));
P("qwi", "test/BannedSymbols.covered.txt", M("System.Threading.ThreadPool").Where(m => m.Name == "QueueUserWorkItem"));
P("uqwi", "test/BannedSymbols.covered.txt", M("System.Threading.ThreadPool").Where(m => m.Name == "UnsafeQueueUserWorkItem"));

if (args is ["check"])
{
    var repo = Path.GetDirectoryName(Path.GetDirectoryName((string)AppContext.GetData("EntryPointFilePath")!))!;
    string[] lists = ["BannedSymbols.txt", "test/BannedSymbols.txt", "test/BannedSymbols.covered.txt"];
    var listed = lists
        .SelectMany(f => File.ReadLines(Path.Combine(repo, f)).Select(l => (f, id: l.Split(';')[0].Trim())))
        .Where(e => e.id.Length > 0 && !e.id.StartsWith("//", StringComparison.Ordinal))
        .ToHashSet();
    var errors = listed
        .Where(e => DocumentationCommentId.GetSymbolsForDeclarationId(e.id, c).IsEmpty)
        .Select(e => $"{e.f}: {e.id} resolves to no symbol")
        .Concat(gen.Where(g => !listed.Contains((g.List, g.Id))).Select(g => $"{g.Group}: {g.Id} is generated but not banned in {g.List}"))
        .ToList();
    foreach (var e in errors)
    {
        Console.Error.WriteLine(e);
    }

    Console.WriteLine(errors.Count == 0 ? "All banned IDs resolve and every generated ID is banned." : $"{errors.Count} banned-list errors.");
    return errors.Count == 0 ? 0 : 1;
}

foreach (var g in gen.GroupBy(g => g.Group))
{
    Console.WriteLine($"# {g.Key} ({g.First().List})");
    foreach (var e in g)
    {
        Console.WriteLine(e.Id);
    }
}

return 0;
