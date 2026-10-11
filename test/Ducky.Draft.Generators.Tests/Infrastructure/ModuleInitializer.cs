using System.Runtime.CompilerServices;
using VerifyTests;

namespace Ducky.Draft.Generators.Tests.Infrastructure;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VerifySourceGenerators.Initialize();
        VerifierSettings.UseUtf8NoBom(); // .editorconfig: charset = utf-8

        // Stryker's MTP runner reruns the same tests in one reused test-host process; without this,
        // Verify's unique-prefix check fails every rerun of a snapshot test and fakes mutant kills.
        VerifierSettings.DisableRequireUniquePrefix();
    }
}
