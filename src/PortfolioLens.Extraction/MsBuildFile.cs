using System.Xml.Linq;

namespace PortfolioLens.Extraction;

// One loaded MSBuild file: a .csproj, .vbproj, .fsproj, or a props file.
internal sealed class MsBuildFile(XElement root, IReadOnlyDictionary<string, string> properties)
{
    public XElement Root { get; } = root;

    public IReadOnlyDictionary<string, string> Properties { get; } = properties;

    // Empty for SDK-style. "http://schemas.microsoft.com/developer/msbuild/2003" for legacy.
    public XNamespace Ns => Root.Name.Namespace;

    public static async Task<MsBuildFile> LoadAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        var root = doc.Root!;
        return new MsBuildFile(root, ReadProperties(root));
    }

    // Unconditional value: the last one wins, as in MSBuild.
    // Conditional value: we do not evaluate it. We use it only if there is no other value.
    private static Dictionary<string, string> ReadProperties(XElement root)
    {
        var ns = root.Name.Namespace;
        var sure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var maybe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in root.Elements(ns + "PropertyGroup"))
        {
            var groupHasCondition = group.Attribute("Condition") is not null;
            foreach (var property in group.Elements())
            {
                var conditional = groupHasCondition || property.Attribute("Condition") is not null;
                if (conditional)
                {
                    maybe.TryAdd(property.Name.LocalName, property.Value.Trim());
                }
                else
                {
                    sure[property.Name.LocalName] = property.Value.Trim();
                }
            }
        }

        foreach (var (name, value) in maybe)
        {
            sure.TryAdd(name, value);
        }

        return sure;
    }
}