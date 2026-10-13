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

    private const string MutableCollectionId = "DUCKY105";
    private const string MutableCollectionTitle = "[Draftable] record member of a mutable collection type";
    private const string MutableCollectionMessage = "'{0}' is a plain member of the draft: its type '{1}' is a mutable collection, so structural sharing and immutability can't be guaranteed";
    private const string MutableCollectionHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY105.md";

    private const string EscapeId = "DUCKY106";
    private const string EscapeTitle = "Draft escapes its Produce recipe";
    private const string EscapeMessage = "'{0}' escapes its Produce recipe: the draft is revoked when Produce returns, so any later use throws ObjectDisposedException";
    private const string EscapeHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY106.md";

    private const string NoCompatibleDraftId = "DUCKY107";
    private const string NoCompatibleDraftTitle = "Member type is [Draftable] in metadata but has no compatible Draft";
    private const string NoCompatibleDraftMessage = "'{0}' gets no draft of '{1}': '{1}' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<{1}> with a public Draft({1}) constructor; it is edited by assignment";
    private const string NoCompatibleDraftHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY107.md";

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

    public static readonly DiagnosticDescriptor MutableCollection = new(
        MutableCollectionId, MutableCollectionTitle, MutableCollectionMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: MutableCollectionHelp);

    public static readonly DiagnosticDescriptor Escape = new(
        EscapeId, EscapeTitle, EscapeMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: EscapeHelp);

    public static readonly DiagnosticDescriptor NoCompatibleDraft = new(
        NoCompatibleDraftId, NoCompatibleDraftTitle, NoCompatibleDraftMessage, Category, DiagnosticSeverity.Info, Enabled, helpLinkUri: NoCompatibleDraftHelp);

    public static readonly DiagnosticDescriptor ErrorObsolete = new(
        ErrorObsoleteId, ErrorObsoleteTitle, ErrorObsoleteMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: ErrorObsoleteHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = [NotPartialRecord, ContainerNotPartial, Generic, NameClash, MutableCollection, Escape, NoCompatibleDraft, ErrorObsolete];
}
