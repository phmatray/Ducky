using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DocFX;
using Serilog;

partial class Build
{
    AbsolutePath DocfxConfig
        => RootDirectory / "docs" / "docfx.json";
    
    AbsolutePath DocfxSite
        => RootDirectory / "docs" / "_site";
    
    Target UpdateDocumentation => _ => _
        .Executes(() =>
        {
            Log.Information("Generating documentation with DocFX...");

            // Build documentation
            DocFXTasks.DocFXBuild(s => s
                .SetConfigFile(DocfxConfig)
                .SetOutputFolder(DocfxSite));

            Log.Information("Documentation generated successfully at '{0}'.", DocfxSite);
        });
}
