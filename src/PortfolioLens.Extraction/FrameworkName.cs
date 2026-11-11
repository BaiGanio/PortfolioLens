using NuGet.Frameworks;

namespace PortfolioLens.Extraction;

internal static class FrameworkName
{
    // "net10.0" -> "net10.0", "netcoreapp3.1" -> "netcoreapp3.1", "NET472" -> "net472"
    public static string? FromMoniker(string moniker)
    {
        var framework = NuGetFramework.Parse(moniker.Trim());
        return framework.IsUnsupported ? null : framework.GetShortFolderName();
    }

    // "v4.7.2" -> "net472"
    public static string? FromLegacyVersion(string targetFrameworkVersion)
    {
        var text = targetFrameworkVersion.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version)
            ? new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, version).GetShortFolderName()
            : null;
    }
}