namespace PortfolioLens.Extraction;

internal static class PathText
{
    // Always '/', on each OS. The output must be the same on Windows and on a Mac.
    public static string Relative(string appRoot, string fullPath) =>
        Path.GetRelativePath(appRoot, fullPath).Replace('\\', '/');
}