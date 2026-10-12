using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.LifetimeAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY004 (SPEC §7, §16.2, ADR-0025): a Singleton store in a web project that is not Blazor WebAssembly. The project
// kind comes from the CompilerVisibleProperty items of buildTransitive/Ducky.targets, supplied here as the
// build_property.* entries of a global analyzer config, as the SDK writes them.
public sealed class SingletonLifetimeTests
{
    private const string Server = "build_property.UsingMicrosoftNETSdkWeb = true\n";

    private static string Source(string body) => $$"""
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        public sealed class Other { public ServiceLifetime Lifetime { get; set; } }

        public static class Startup
        {
            private static ServiceLifetime _field;

            public static void Configure(IServiceCollection services) => services.AddDucky(b => { {{body}} });
        }
        """;

    private static Task VerifyAsync(string config, string body, params DiagnosticResult[] expected)
    {
        var test = Verifier.Test(Source(body));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", "is_global = true\n" + config));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static TheoryData<string, string> Reported => new()
    {
        { Server, "{|#0:b.Lifetime = ServiceLifetime.Singleton|};" },
        { Server, "{|#0:b.Lifetime = 0|};" },
        { Server, "{|#0:b.Lifetime = (ServiceLifetime)0|};" },
        { Server, "{|#0:b.Lifetime = default|};" },
        { Server + "build_property.UsingMicrosoftNETSdkBlazorWebAssembly = false\n", "{|#0:b.Lifetime = ServiceLifetime.Singleton|};" },
        { "build_property.UsingMicrosoftNETSdkWeb = True\nbuild_property.UsingMicrosoftNETSdkBlazorWebAssembly = \n", "{|#0:b.Lifetime = ServiceLifetime.Singleton|};" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky004_SingletonInServerProject_Reported(string config, string body) =>
        VerifyAsync(config, body, Verifier.Diagnostic(UsageDiagnostics.SingletonOnServer).WithLocation(0));

    public static TheoryData<string, string> NotReported => new()
    {
        // Not a server project: no web SDK, a non-web value, or Blazor WebAssembly (any case).
        { "", "b.Lifetime = ServiceLifetime.Singleton;" },
        { "build_property.UsingMicrosoftNETSdkWeb = false\n", "b.Lifetime = ServiceLifetime.Singleton;" },
        { "build_property.UsingMicrosoftNETSdkWeb = truer\n", "b.Lifetime = ServiceLifetime.Singleton;" },
        { Server + "build_property.UsingMicrosoftNETSdkBlazorWebAssembly = true\n", "b.Lifetime = ServiceLifetime.Singleton;" },
        { Server + "build_property.UsingMicrosoftNETSdkBlazorWebAssembly = TRUE\n", "b.Lifetime = ServiceLifetime.Singleton;" },

        // Another lifetime, a value known only at run time, another builder property, another type's Lifetime, a field.
        { Server, "b.Lifetime = ServiceLifetime.Scoped;" },
        { Server, "b.Lifetime = System.OperatingSystem.IsBrowser() ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;" },
        { Server, "b.MaxDispatchDepth = 0;" },
        { Server, "new Other().Lifetime = ServiceLifetime.Singleton;" },
        { Server, "_field = ServiceLifetime.Singleton;" },
    };

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky004_NotServerOrNotSingleton_NotReported(string config, string body) => VerifyAsync(config, body);
}
