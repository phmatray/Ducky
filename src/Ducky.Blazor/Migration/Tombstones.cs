using System.ComponentModel;

// SPEC §22.1, ADR-0038: the Ducky.Blazor 1.0.292 tombstone; it coexists with the non-generic DuckyLayout. Removed in 3.0.
namespace Ducky.Blazor;

#pragma warning disable CA1715 // justification: tombstones keep the published 1.x names of 1.x classes (SPEC §22.1)
/// <summary>Removed in Ducky 2.0 (DUCKYM008): derive from the non-generic <see cref="DuckyLayout"/>.</summary>
/// <typeparam name="TState">The 1.x layout state.</typeparam>
[Obsolete("Ducky 2.0 removed DuckyLayout<TState>: derive from the non-generic DuckyLayout and call Select in OnInitialized.", error: true, DiagnosticId = "DUCKYM008", UrlFormat = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{0}.md")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface DuckyLayout<TState>;
#pragma warning restore CA1715
