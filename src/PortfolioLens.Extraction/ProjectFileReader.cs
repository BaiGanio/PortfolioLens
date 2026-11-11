using System.Xml;
using System.Xml.Linq;
using PortfolioLens.Core.Facts;

namespace PortfolioLens.Extraction;

// Reads the facts of one project file. One instance per app.
internal sealed class ProjectFileReader(string appRoot)
{
    public async Task<ProjectReadResult> ReadAsync(string projectPath, CancellationToken ct)
    {
        var relativePath = PathText.Relative(appRoot, projectPath);

        MsBuildFile file;
        try
        {
            file = await MsBuildFile.LoadAsync(projectPath, ct);
        }
        catch (XmlException ex)
        {
            var error = new ScanError(relativePath, ScanErrorKind.InvalidXml, ex.Message);
            return new ProjectReadResult(null, [error]);
        }

        var errors = new List<ScanError>();

        var sdk = FindSdk(file.Root, file.Ns);
        var frameworks = ReadFrameworks(file.Properties);
        if (frameworks.Count == 0)
        {
            errors.Add(new ScanError(relativePath, ScanErrorKind.Unresolved, "No target framework found"));
        }

        var project = new ProjectFacts(
            Path: relativePath,
            Language: LanguageOf(projectPath),
            Style: sdk is null ? ProjectStyle.Legacy : ProjectStyle.Sdk,
            Sdk: sdk,
            OutputType: file.Properties.GetValueOrDefault("OutputType"),
            TargetFrameworks: frameworks,
            Packages: [],            // step 6
            ProjectReferences: [],   // step 6
            AssemblyReferences: []); // step 6

        return new ProjectReadResult(project, errors);
    }

    private static ProjectLanguage LanguageOf(string projectPath)
    {
        var extension = Path.GetExtension(projectPath);
        if (extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectLanguage.VisualBasic;
        }

        if (extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectLanguage.FSharp;
        }

        return ProjectLanguage.CSharp;
    }

    // YOU WRITE THIS. Rules: see "FindSdk" below.
    private static string? FindSdk(XElement root, XNamespace ns)
    {
        throw new NotImplementedException();
    }

    // YOU WRITE THIS. Rules: see "ReadFrameworks" below.
    private static List<string> ReadFrameworks(IReadOnlyDictionary<string, string> properties)
    {
        throw new NotImplementedException();
    }
}