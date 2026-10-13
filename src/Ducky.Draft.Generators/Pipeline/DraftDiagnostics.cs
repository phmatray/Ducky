using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators;

/// <summary>
/// The Draft generator descriptors (SPEC §16.2). Static readonly fields, as release tracking requires (a property-created
/// descriptor is RS2002). Their initializers run once per test host, before Stryker activates any mutant, so every
/// literal is a constant, which Stryker doesn't mutate; <c>Descriptors_MatchCatalogue</c> pins each value.
/// </summary>
internal static class DraftDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string NotPartialRecordId = "DUCKY101";
    private const string NotPartialRecordTitle = "[Draftable] type is not a partial record class";
    private const string NotPartialRecordMessage = "'{0}' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)";
    private const string NotPartialRecordHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY101.md";

    private const string ContainerNotPartialId = "DUCKY102";
    private const string ContainerNotPartialTitle = "Containing type of a [Draftable] record is not partial";
    private const string ContainerNotPartialMessage = "'{0}' gets no draft: its containing type '{1}' is not partial";
    private const string ContainerNotPartialHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY102.md";

    private const string GenericId = "DUCKY103";
    private const string GenericTitle = "Generic [Draftable] record";
    private const string GenericMessage = "'{0}' gets no draft: it is generic or nested in a generic type, which Ducky.Draft 2.0 does not support";
    private const string GenericHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY103.md";

    private const string NameClashId = "DUCKY104";
    private const string NameClashTitle = "[Draftable] record declares a generated member name";
    private const string NameClashMessage = "'{0}' gets no draft: it already declares a member named '{1}', which the generator emits";
    private const string NameClashHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY104.md";

    private const string ErrorObsoleteId = "DUCKY108";
    private const string ErrorObsoleteTitle = "[Draftable] record is error-obsolete";
    private const string ErrorObsoleteMessage = "'{0}' gets no draft: it is marked [Obsolete(error: true)], so generated code can't reference it (CS0619)";
    private const string ErrorObsoleteHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY108.md";

    public static readonly DiagnosticDescriptor NotPartialRecord = new(
        NotPartialRecordId, NotPartialRecordTitle, NotPartialRecordMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: NotPartialRecordHelp);

    public static readonly DiagnosticDescriptor ContainerNotPartial = new(
        ContainerNotPartialId, ContainerNotPartialTitle, ContainerNotPartialMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: ContainerNotPartialHelp);

    public static readonly DiagnosticDescriptor Generic = new(
        GenericId, GenericTitle, GenericMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: GenericHelp);

    public static readonly DiagnosticDescriptor NameClash = new(
        NameClashId, NameClashTitle, NameClashMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: NameClashHelp);

    public static readonly DiagnosticDescriptor ErrorObsolete = new(
        ErrorObsoleteId, ErrorObsoleteTitle, ErrorObsoleteMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: ErrorObsoleteHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = [NotPartialRecord, ContainerNotPartial, Generic, NameClash, ErrorObsolete];
}
