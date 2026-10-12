using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.BlockingAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY010 (SPEC §7, §16.2, ADR-0014): blocking on DispatchAsync, WhenIdleAsync or InitializeAsync of Ducky,
// Ducky.Blazor or Ducky.Testing with .Result, .Wait() or GetAwaiter().GetResult(), reported at the blocking expression.
// The SetAsync arm ships with M9-01c.
public sealed class BlockingTests
{
    private static string Source(string body) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record Ping;
        public sealed class Other
        {
            public Task<int> DispatchAsync() => Task.FromResult(1);
            public static Task Wrap(Task task) => task;
            public static System.Runtime.CompilerServices.TaskAwaiter Await(Task task) => task.GetAwaiter();
            public static void GetResult() { }
        }

        public static class Calls
        {
            public static void Run(IStore store, IDispatcher dispatcher, EffectContext ctx, Middleware middleware)
            {
                {{body}}
            }
        }
        """;

    public static TheoryData<string, string, string> Reported => new()
    {
        { "_ = {|#0:store.DispatchAsync(new Ping()).Result|};", "DispatchAsync", ".Result" },
        { "{|#0:store.DispatchAsync(new Ping()).Wait()|};", "DispatchAsync", ".Wait()" },
        { "_ = {|#0:dispatcher.DispatchAsync(new Ping()).Wait(1000)|};", "DispatchAsync", ".Wait()" },
        { "_ = {|#0:store.DispatchAsync(new Ping()).GetAwaiter().GetResult()|};", "DispatchAsync", "GetAwaiter().GetResult()" },
        { "_ = {|#0:ctx.DispatchAsync(new Ping()).Result|};", "DispatchAsync", ".Result" },
        { "{|#0:store.WhenIdleAsync().GetAwaiter().GetResult()|};", "WhenIdleAsync", "GetAwaiter().GetResult()" },
        { "{|#0:store.WhenIdleAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult()|};", "WhenIdleAsync", "GetAwaiter().GetResult()" },
        { "{|#0:store.InitializeAsync().Wait()|};", "InitializeAsync", ".Wait()" },
        { "{|#0:middleware.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult()|};", "InitializeAsync", "GetAwaiter().GetResult()" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky010_BlockingOnDuckyTask_Reported(string body, string method, string how) =>
        Verifier.VerifyAsync(Source(body), Verifier.Diagnostic(UsageDiagnostics.Blocking).WithLocation(0).WithArguments(method, how));

    public static TheoryData<string> NotReported =>
    [
        // Fire-and-forget and other members of the task or its awaiter.
        "_ = store.DispatchAsync(new Ping());",
        "_ = store.DispatchAsync(new Ping()).IsCompleted;",
        "_ = store.DispatchAsync(new Ping()).ContinueWith(t => t.Result);",
        "_ = store.DispatchAsync(new Ping()).GetAwaiter().IsCompleted;",
        "store.Dispatch(new Ping());",

        // Blocking on a task that isn't Ducky's, or that a non-Ducky call wraps.
        "_ = Task.FromResult(1).Result;",
        "_ = new Other().DispatchAsync().Result;",
        "Other.Wrap(store.WhenIdleAsync()).GetAwaiter().GetResult();",
        "Task.Delay(1).Wait();",
        "default(System.Runtime.CompilerServices.TaskAwaiter).GetResult();",
        "Other.Await(store.WhenIdleAsync()).GetResult();",
        "Other.GetResult();",

        // Another Ducky method that returns a task.
        "ctx.Run(new Ping(), _ => Task.FromResult(1), _ => new Ping(), _ => new Ping(), CancellationToken.None).Wait();",
    ];

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky010_NotBlockingOnDuckyTask_NotReported(string body) => Verifier.VerifyAsync(Source(body));

    // Ducky.Blazor's protected DispatchAsync of a component, called from a handler.
    [Fact]
    public Task Ducky010_ComponentDispatchAsync_Reported() =>
        Verifier.VerifyAsync(
            """
            public sealed record Ping;
            public sealed class Counter : Ducky.Blazor.DuckyComponent
            {
                public void Increment() => _ = {|#0:DispatchAsync(new Ping()).Result|};
            }
            """,
            Verifier.Diagnostic(UsageDiagnostics.Blocking).WithLocation(0).WithArguments("DispatchAsync", ".Result"));

    // Ducky.Testing's TestStore forwards to the store, so blocking on its tasks deadlocks the same way.
    [Theory]
    [InlineData("_ = {|#0:testStore.DispatchAsync(new Ping()).Result|};", "DispatchAsync", ".Result")]
    [InlineData("{|#0:testStore.WhenIdleAsync().Wait()|};", "WhenIdleAsync", ".Wait()")]
    [InlineData("{|#0:testStore.InitializeAsync().GetAwaiter().GetResult()|};", "InitializeAsync", "GetAwaiter().GetResult()")]
    public Task Ducky010_BlockingOnTestStoreTask_Reported(string body, string method, string how)
    {
        var test = Verifier.Test($$"""
            public sealed record Ping;
            public static class Calls
            {
                public static void Run(Ducky.Testing.TestStore testStore)
                {
                    {{body}}
                }
            }
            """);
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Ducky.Testing.dll")));
        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(UsageDiagnostics.Blocking).WithLocation(0).WithArguments(method, how));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    // Without Ducky and Ducky.Blazor references, no method is Ducky's.
    [Fact]
    public Task Ducky010_NoDuckyReference_NotReported()
    {
        var test = Verifier.Test("""
            public sealed class Store { public System.Threading.Tasks.Task<int> DispatchAsync() => System.Threading.Tasks.Task.FromResult(1); }
            public static class Calls { public static int Run(Store store) => store.DispatchAsync().Result; }
            """);
        test.TestState.AdditionalReferences.RemoveAll(r => Path.GetFileName(r.Display)!.StartsWith("Ducky", StringComparison.Ordinal));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
