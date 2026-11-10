using System.IO.Enumeration;

namespace PortfolioLens.Extraction;

public static class ProjectFileWalker
{
    private static readonly HashSet<string> SkipFolders = 
        new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules", "packages"
        };

    private static readonly HashSet<string> Extensions = 
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".sln", ".slnx"
        };

    public static IEnumerable<string> Find(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        return new FileSystemEnumerable<string>(root, (ref FileSystemEntry entry) => entry.ToFullPath(), options)
        {
            ShouldIncludePredicate = (ref entry) => !entry.IsDirectory && Extensions.Contains(Path.GetExtension(entry.FileName).ToString()),
            ShouldRecursePredicate = (ref entry) => !SkipFolders.Contains(entry.FileName.ToString())
        };
    }
}