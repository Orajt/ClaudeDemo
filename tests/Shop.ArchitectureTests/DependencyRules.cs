using System.Text.RegularExpressions;

namespace Shop.ArchitectureTests;

/// <summary>
/// Reguły zależności z docs/architecture.md §3 liczone na referencjach z plików csproj.
/// Zwraca listę naruszeń (z nazwą projektu i zakazanej zależności) – pusta lista oznacza zgodność.
/// </summary>
public static partial class DependencyRules
{
    private const string SharedKernel = "Shop.SharedKernel";
    private const string GeneratedApiContract = "Shop.Api.Contracts.Generated";

    // Domain: czysty C#. Wyjątek JasperFx.Events – analizator (source generator) Martena musi działać w assembly z agregatem (D-009).
    private static readonly string[] DomainForbiddenPackagePrefixes =
        ["Marten", "WolverineFx", "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "Aspire"];

    private static readonly string[] ApiForbiddenPackagePrefixes =
        ["Marten", "WolverineFx.Marten", "WolverineFx.EntityFrameworkCore", "Microsoft.EntityFrameworkCore", "Npgsql"];

    public static IReadOnlyList<string> FindViolations(IReadOnlyCollection<ProjectFile> projects) =>
        projects.SelectMany(ViolationsOf).ToList();

    private static IEnumerable<string> ViolationsOf(ProjectFile project)
    {
        var parsed = ModuleProjectName.TryParse(project.Name);
        if (parsed is null)
            return [];

        return ForbiddenProjects(project, parsed).Concat(ForbiddenPackages(project, parsed));
    }

    private static IEnumerable<string> ForbiddenProjects(ProjectFile project, ModuleProjectName name)
    {
        foreach (var reference in project.ProjectReferences.Where(reference => !IsAllowedProject(name, reference)))
            yield return $"{project.Name} referencjonuje projekt {reference} – niedozwolone dla warstwy {name.Layer} modułu {name.Module}.";
    }

    private static IEnumerable<string> ForbiddenPackages(ProjectFile project, ModuleProjectName name)
    {
        var forbiddenPrefixes = name.Layer switch
        {
            "Domain" => DomainForbiddenPackagePrefixes,
            "Api" => ApiForbiddenPackagePrefixes,
            _ => []
        };

        foreach (var package in project.PackageReferences.Where(package => IsForbiddenPackage(name.Layer, package, forbiddenPrefixes)))
            yield return $"{project.Name} referencjonuje pakiet {package} – niedozwolone dla warstwy {name.Layer}.";
    }

    private static bool IsForbiddenPackage(string layer, string package, string[] forbiddenPrefixes)
    {
        var isDomainAnalyzerException = layer == "Domain" && package.StartsWith("JasperFx.Events", StringComparison.Ordinal);
        return !isDomainAnalyzerException && forbiddenPrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.Ordinal));
    }

    // Lista dozwolonych projektów per warstwa: wszystko spoza listy jest naruszeniem.
    private static bool IsAllowedProject(ModuleProjectName from, string reference)
    {
        if (reference == SharedKernel)
            return from.Layer is "Domain" or "Application" or "Infrastructure" or "Api";

        var target = ModuleProjectName.TryParse(reference);
        if (reference == GeneratedApiContract)
            return from.Layer == "Api";

        if (target is null)
            return false;

        var isOwnModule = target.Module == from.Module;
        return from.Layer switch
        {
            "Domain" => false,
            "Contracts" => false,
            "Migrations" => false,
            "Application" => (isOwnModule && target.Layer is "Domain" or "Contracts") || (!isOwnModule && target.Layer == "Contracts"),
            "Infrastructure" => isOwnModule && target.Layer is "Application" or "Domain" or "Contracts",
            "Api" => isOwnModule && target.Layer == "Application",
            _ => false
        };
    }

    /// <summary>Nazwa projektu w formacie Shop.&lt;Moduł&gt;.&lt;Warstwa&gt;.</summary>
    private sealed record ModuleProjectName(string Module, string Layer)
    {
        public static ModuleProjectName? TryParse(string projectName)
        {
            var match = ModuleProjectPattern().Match(projectName);
            return match.Success ? new ModuleProjectName(match.Groups["module"].Value, match.Groups["layer"].Value) : null;
        }
    }

    [GeneratedRegex(@"^Shop\.(?<module>Orders|Settlements|DataHub)\.(?<layer>Contracts|Domain|Application|Infrastructure|Migrations|Api)$")]
    private static partial Regex ModuleProjectPattern();
}
