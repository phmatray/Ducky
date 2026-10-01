using System.Diagnostics.CodeAnalysis;

namespace Client;

// Every Routes.razor trips the trim analysis in 10.0.12 through the generated OpenComponent<Router>, whose DAM marks
// Router.NotFoundPage (a DAM-annotated property): IL2111 from the analyzer, IL2110 from ILLink once the assembly is a
// trimming root. Ducky.Blazor renders no Router; the seed path is what S-3 measures.
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "Router.NotFoundPage, framework")]
[UnconditionalSuppressMessage("Trimming", "IL2110", Justification = "Router.NotFoundPage, framework")]
public partial class Routes;
