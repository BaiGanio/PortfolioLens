namespace PortfolioLens.Core.Facts;

public enum ScanErrorKind { InvalidXml, Unresolved, Io, Unexpected }
public enum PackageSource { PackageReference, CentralPackage, PackagesConfig }
public enum ProjectStyle { Sdk, Legacy }
public enum ProjectLanguage { CSharp, VisualBasic, FSharp }

public sealed record ScanError(string Path, ScanErrorKind Kind, string Message);
public sealed record AssemblyRef(string Name, string? HintPath);
public sealed record PackageRef(string Id, string? Version, PackageSource Source, string? Condition);

public sealed record ProjectFacts(
    string Path,
    ProjectLanguage Language,
    ProjectStyle Style,
    string? Sdk,
    string? OutputType,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<PackageRef> Packages,
    IReadOnlyList<string>ProjectReferences,
    IReadOnlyList<AssemblyRef> AssemblyReferences 
);

public sealed record AppFacts(
    string Name,
    IReadOnlyList<string> Solutions,
    IReadOnlyList<ProjectFacts> Projects,
    IReadOnlyList<ScanError> Errors
);