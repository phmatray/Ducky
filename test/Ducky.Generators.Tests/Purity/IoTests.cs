using Ducky.Generators.Analyzers.Purity;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// DUCKY002 (SPEC §16.2), reducer and projector arms: I/O and blocking are reported at their use, with the member (or
// `await`) and the arm as arguments.
public sealed class IoTests
{
    public static TheoryData<string, string, string> Reported
    {
        get
        {
            var rows = new TheoryData<string, string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                rows.Add(arm, "s with { Count = {|#0:File.Exists(\"x\")|} ? 1 : 0 }", "File.Exists");
                rows.Add(arm, "{ using var stream = {|#0:new MemoryStream()|}; return s; }", "new MemoryStream");
                rows.Add(arm, "s with { Count = {|#0:System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(\"a\", \"b\")|} ? 1 : 0 }", "FileSystemName.MatchesSimpleExpression");
                rows.Add(arm, "s with { Count = {|#0:http.GetAsync(\"x\")|}.Id }", "HttpClient.GetAsync");
                rows.Add(arm, "s with { Count = {|#0:http.GetFromJsonAsync<int>(\"x\")|}.Id }", "HttpClient.GetFromJsonAsync");
                rows.Add(arm, "s with { Count = {|#0:js.InvokeAsync<int>(\"f\")|}.GetHashCode() }", "IJSRuntime.InvokeAsync");
                rows.Add(arm, "s with { Count = {|#0:js.InvokeVoidAsync(\"f\")|}.GetHashCode() }", "IJSRuntime.InvokeVoidAsync");
                rows.Add(arm, "s with { Count = {|#0:((IJSInProcessRuntime)js).Invoke<int>(\"f\")|} }", "IJSInProcessRuntime.Invoke");
                rows.Add(arm, "{ {|#0:Console.WriteLine()|}; return s; }", "Console.WriteLine");
                rows.Add(arm, "s with { Count = {|#0:task.Result|} }", "Task.Result");
                rows.Add(arm, "s with { Count = {|#0:new ValueTask<int>(1).Result|} }", "ValueTask.Result");
                rows.Add(arm, "{ {|#0:task.Wait()|}; return s; }", "Task.Wait");
                rows.Add(arm, "s with { Count = {|#0:task.GetAwaiter().GetResult()|} }", "TaskAwaiter.GetResult");
                rows.Add(arm, "s with { Count = {|#0:task.ConfigureAwait(false).GetAwaiter().GetResult()|} }", "ConfiguredTaskAwaiter.GetResult");
                rows.Add(arm, "{ Func<Task<int>> later = async () => {|#0:await task|}; return s; }", "await");
                rows.Add(arm, "{ Func<Task> later = async () => { {|#0:await|} foreach (var i in (IAsyncEnumerable<int>)log) { } }; return s; }", "await");
                rows.Add(arm, "{ Func<Task> later = async () => { {|#0:await|} using (default(IAsyncDisposable)!) { } }; return s; }", "await");
                rows.Add(arm, "{ Func<Task> later = async () => { {|#0:await|} using var d = default(IAsyncDisposable)!; }; return s; }", "await");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky002_IoOrBlocking_Reported(string arm, string body, string member) =>
        Verifier.VerifyAsync(Host(arm, body), Verifier.Diagnostic(PurityDiagnostics.Io).WithLocation(0).WithArguments(member, arm));

    public static TheoryData<string, string> Silent
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                rows.Add(arm, "s with { Count = task.IsCompleted ? Task.FromResult(1).Id : task.GetAwaiter().IsCompleted.GetHashCode() }");
                rows.Add(arm, "s with { Count = string.Concat(\"a\", \"b\").Length + s.Items.Count + Other.IOLike.Read() + s.Array.AsSpan().Length }");
                // nameof evaluates nothing.
                rows.Add(arm, "s with { Count = nameof(task.Result).Length + nameof(File.Exists).Length + nameof(Console).Length }");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky002_PureMember_NotReported(string arm, string body) =>
        Verifier.VerifyAsync(Host(arm, body) + """

            namespace Other { public static class IOLike { public static int Read() => 0; } }
            """);

    // `System.IO.*` is a namespace and its children, never a namespace that merely starts with the same letters.
    [Theory]
    [MemberData(nameof(Arms), MemberType = typeof(PuritySources))]
    public Task Ducky002_LookalikeNamespace_NotReported(string arm) =>
        Verifier.VerifyAsync(Host(arm, "s with { Count = System.IOX.Probe.Read() }") + """

            namespace System.IOX { public static class Probe { public static int Read() => 0; } }
            """);
}
