using PortfolioLens.Core.Facts;
using PortfolioLens.Extraction;

namespace PortfolioLens.UnitTests;

public class ProjectFileReaderTests
{
    // Reads one project file of one fixture. The fixture folder is the app root.
    private static Task<ProjectReadResult> Read(string fixture, string file)
    {
        var appRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", fixture);
        var reader = new ProjectFileReader(appRoot);
        return reader.ReadAsync(Path.Combine(appRoot, file), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sdk_style_project_has_sdk_and_net10()
    {
        var result = await Read("sdk-style-net10", "SdkStyle.csproj");

        var project = Assert.IsType<ProjectFacts>(result.Project);
        Assert.Equal(ProjectStyle.Sdk, project.Style);
        Assert.Equal(["net10.0"], project.TargetFrameworks);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Legacy_project_gets_short_framework_name()
    {
        var result = await Read("old-style-net472", "OldStyle.csproj");

        var project = Assert.IsType<ProjectFacts>(result.Project);
        Assert.Equal(ProjectStyle.Legacy, project.Style);
        Assert.Null(project.Sdk);
        Assert.Equal(["net472"], project.TargetFrameworks);
    }

    [Fact]
    public async Task Multi_target_project_has_three_sorted_frameworks()
    {
        var result = await Read("multi-target", "MultiTarget.csproj");

        var project = Assert.IsType<ProjectFacts>(result.Project);
        Assert.Equal(["net10.0", "net472", "netstandard2.0"], project.TargetFrameworks);
    }

    [Fact]
    public async Task Vb_project_is_visual_basic_on_net40()
    {
        var result = await Read("vbproj-legacy", "OldVb.vbproj");

        var project = Assert.IsType<ProjectFacts>(result.Project);
        Assert.Equal(ProjectLanguage.VisualBasic, project.Language);
        Assert.Equal(["net40"], project.TargetFrameworks);
    }

    [Fact]
    public async Task Broken_xml_gives_an_error_and_no_project()
    {
        var result = await Read("broken-xml", "Broken.csproj");

        Assert.Null(result.Project);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ScanErrorKind.InvalidXml, error.Kind);
        Assert.Equal("Broken.csproj", error.Path);
    }
}