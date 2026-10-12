using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Analyzers.Usage;

/// <summary>
/// The DUCKY004, 006, 008, 010, 016 and 025 descriptors (SPEC §16.2). Static readonly fields over constants, as in
/// RegistrationDiagnostics: release tracking needs the fields, Stryker doesn't mutate constants, and
/// <c>Descriptors_MatchCatalogue</c> pins each value.
/// </summary>
internal static class UsageDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string SingletonOnServerId = "DUCKY004";
    private const string SingletonOnServerHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY004.md";
    private const string SingletonOnServerTitle = "Singleton store in a server project";
    private const string SingletonOnServerMessage =
        "'Lifetime = ServiceLifetime.Singleton' in a web project that is not Blazor WebAssembly shares one store between every user of the server: keep the default lifetime (Scoped)";

    private const string NotCoveredId = "DUCKY006";
    private const string NotCoveredHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY006.md";
    private const string NotCoveredTitle = "Type missing from the JSON context";
    private const string NotCoveredMessage = "'{0}' is not covered by the JSON context passed to UseJson: add [JsonSerializable(typeof({0}))] to it";

    private const string StoreInjectedId = "DUCKY008";
    private const string StoreInjectedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY008.md";
    private const string StoreInjectedTitle = "Store injected into an effect or middleware constructor";
    private const string StoreInjectedMessage = "'{0}' injects '{1}' into its constructor, where using it would re-enter materialization: {2}";

    private const string BlockingId = "DUCKY010";
    private const string BlockingHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY010.md";
    private const string BlockingTitle = "Blocking on a Ducky task";
    private const string BlockingMessage = "Blocking on '{0}' with '{1}' can deadlock the dispatcher: await it instead";

    private const string RegistrationNotCalledId = "DUCKY016";
    private const string RegistrationNotCalledHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY016.md";
    private const string RegistrationNotCalledTitle = "Generated registration never called";
    private const string RegistrationNotCalledMessage =
        "This assembly defines slices or effects but never calls 'AddDuckyGenerated_{0}': call b.AddDuckyGenerated_{0}() in AddDucky to register them";

    private const string OptionsDroppedId = "DUCKY025";
    private const string OptionsDroppedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY025.md";
    private const string OptionsDroppedTitle = "JsonTypeInfoResolver.Combine drops the context options";
    private const string OptionsDroppedMessage =
        "'{0}' sets [JsonSourceGenerationOptions] that JsonTypeInfoResolver.Combine drops, so they would not apply to persisted, seed and DevTools JSON: use UseJson(new JsonSerializerOptions({0}.Default.Options) {{ TypeInfoResolver = {1} }})";

    public static readonly DiagnosticDescriptor SingletonOnServer = new(
        SingletonOnServerId, SingletonOnServerTitle, SingletonOnServerMessage, Category, DiagnosticSeverity.Warning, Enabled,
        helpLinkUri: SingletonOnServerHelp);

    public static readonly DiagnosticDescriptor NotCovered = new(
        NotCoveredId, NotCoveredTitle, NotCoveredMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: NotCoveredHelp);

    public static readonly DiagnosticDescriptor StoreInjected = new(
        StoreInjectedId, StoreInjectedTitle, StoreInjectedMessage, Category, DiagnosticSeverity.Warning, Enabled,
        helpLinkUri: StoreInjectedHelp);

    public static readonly DiagnosticDescriptor Blocking = new(
        BlockingId, BlockingTitle, BlockingMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: BlockingHelp);

    // Reported once the whole compilation is seen (RS1037).
    public static readonly DiagnosticDescriptor RegistrationNotCalled = new(
        RegistrationNotCalledId, RegistrationNotCalledTitle, RegistrationNotCalledMessage, Category, DiagnosticSeverity.Warning, Enabled,
        helpLinkUri: RegistrationNotCalledHelp, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor OptionsDropped = new(
        OptionsDroppedId, OptionsDroppedTitle, OptionsDroppedMessage, Category, DiagnosticSeverity.Warning, Enabled,
        helpLinkUri: OptionsDroppedHelp);
}
