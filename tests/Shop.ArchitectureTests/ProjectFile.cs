using System.Xml.Linq;

namespace Shop.ArchitectureTests;

/// <summary>Zależności jednego projektu odczytane z pliku csproj: nazwy projektów i pakietów.</summary>
public sealed record ProjectFile(
    string Name,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<string> PackageReferences)
{
    public static ProjectFile Read(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);

        return new ProjectFile(
            Path.GetFileNameWithoutExtension(csprojPath),
            IncludesOf(document, "ProjectReference").Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/'))).ToList(),
            IncludesOf(document, "PackageReference").ToList());
    }

    public static IReadOnlyList<ProjectFile> ReadAllFromSources() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryLocator.Root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Read)
            .ToList();

    private static IEnumerable<string> IncludesOf(XDocument document, string elementName) =>
        document.Descendants(elementName).Select(element => (string?)element.Attribute("Include")).OfType<string>();
}
