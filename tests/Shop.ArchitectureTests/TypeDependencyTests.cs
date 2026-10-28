using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Shop.ArchitectureTests;

/// <summary>
/// Reguły zależności na poziomie typów (to, co faktycznie trafia do IL – także zależności przechodnie przez pakiety).
/// Wiele warstw jest dziś puste, dlatego reguły nie wymagają wyników pozytywnych (WithoutRequiringPositiveResults).
/// </summary>
public sealed class TypeDependencyTests
{
    private const string ForbiddenInDomain = @"^(Marten|Wolverine|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Npgsql)(\..*)?$";
    private const string ForbiddenInApi = @"^(Marten|Microsoft\.EntityFrameworkCore|Npgsql)(\..*)?$";

    private static readonly string[] Modules = ["Orders", "Settlements"];

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(AllModuleAssemblies().ToArray())
        .Build();

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    public void Domain_DoesNotDependOnMartenWolverineEfOrAspNet(string module)
    {
        var rule = Types().That().ResideInAssembly($"Shop.{module}.Domain")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(ForbiddenInDomain)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Application_DoesNotDependOnInfrastructureOrApi(string module)
    {
        var rule = Types().That().ResideInAssembly($"Shop.{module}.Application")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(@"^Shop\.\w+\.(Infrastructure|Api)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Application_DoesNotDependOnDomainOrApplicationOfOtherModules(string module)
    {
        var otherModules = string.Join("|", new[] { "Orders", "Settlements", "DataHub" }.Where(other => other != module));

        var rule = Types().That().ResideInAssembly($"Shop.{module}.Application")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace($@"^Shop\.({otherModules})\.(Domain|Application)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Api_DoesNotDependOnInfrastructureMartenOrEf(string module)
    {
        var rule = Types().That().ResideInAssembly($"Shop.{module}.Api")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(
                $@"^Shop\.\w+\.Infrastructure(\..*)?$|{ForbiddenInApi}")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    public void Contracts_DoNotDependOnOtherModuleLayers(string module)
    {
        var rule = Types().That().ResideInAssembly($"Shop.{module}.Contracts")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(
                @"^Shop\.\w+\.(Domain|Application|Infrastructure|Api)(\..*)?$|^Shop\.SharedKernel(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public void DataHub_DoesNotDependOnOtherModulesInternals()
    {
        var rule = Types().That().ResideInNamespace(@"^Shop\.DataHub(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(
                @"^Shop\.(Orders|Settlements)\.(Domain|Application|Infrastructure|Api)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    // Assembly ładowane po nazwie, bo puste warstwy (Domain, Contracts) nie mają żadnego typu do użycia w typeof().
    private static IEnumerable<Assembly> AllModuleAssemblies()
    {
        var names = Modules.SelectMany(module => new[] { "Contracts", "Domain", "Application", "Infrastructure", "Api" }
                .Select(layer => $"Shop.{module}.{layer}"))
            .Concat(["Shop.DataHub.Application", "Shop.DataHub.Infrastructure", "Shop.DataHub.Api"]);

        return names.Select(name => Assembly.Load(name));
    }
}
