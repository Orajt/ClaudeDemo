namespace Shop.ArchitectureTests;

/// <summary>Znajduje korzeń repozytorium (katalog z Shop.slnx), niezależnie od miejsca uruchomienia testów.</summary>
public static class RepositoryLocator
{
    private const string SolutionFileName = "Shop.slnx";

    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException($"Nie znaleziono {SolutionFileName} powyżej {AppContext.BaseDirectory}.");
    }
}
