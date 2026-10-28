using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Shop.ArchitectureTests;

/// <summary>
/// Reguły zależności na poziomie typów (to, co faktycznie trafia do IL – także zależności przechodnie przez pakiety).
/// Wiele warstw jest dziś puste, dlatego reguły nie wymagają wyników pozytywnych (WithoutRequiringPositiveResults).
/// Skoro pusta reguła przechodzi zawsze, testy kontrolne na końcu klasy dowodzą, że wzorce faktycznie coś dopasowują.
/// </summary>
public sealed class TypeDependencyTests
{
    private const string ForbiddenInDomain = @"^(Marten|Wolverine|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Npgsql)(\..*)?$";
    private const string ForbiddenInApi = @"^Shop\.\w+\.Infrastructure(\..*)?$|^(Marten|Microsoft\.EntityFrameworkCore|Npgsql)(\..*)?$";
    private const string ForbiddenInApplication = @"^Shop\.\w+\.(Infrastructure|Api)(\..*)?$";
    private const string ForbiddenInContracts = @"^Shop\.\w+\.(Domain|Application|Infrastructure|Api)(\..*)?$|^Shop\.SharedKernel(\..*)?$";
    private const string DataHubNamespace = @"^Shop\.DataHub(\..*)?$";
    private const string ForbiddenInDataHub = @"^Shop\.(Orders|Settlements)\.(Domain|Application|Infrastructure|Api)(\..*)?$";

    // Warstwa, która dziś na pewno zależy od Martena i od własnych typów – materiał na testy kontrolne.
    private const string OrdersInfrastructure = "Shop.Orders.Infrastructure";

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
        var rule = AssemblyMustNotDependOn($"Shop.{module}.Domain", ForbiddenInDomain);

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Application_DoesNotDependOnInfrastructureOrApi(string module)
    {
        var rule = AssemblyMustNotDependOn($"Shop.{module}.Application", ForbiddenInApplication);

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Application_DoesNotDependOnDomainOrApplicationOfOtherModules(string module)
    {
        var rule = AssemblyMustNotDependOn($"Shop.{module}.Application", OtherModulesDomainOrApplication(module));

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    [InlineData("DataHub")]
    public void Api_DoesNotDependOnInfrastructureMartenOrEf(string module)
    {
        var rule = AssemblyMustNotDependOn($"Shop.{module}.Api", ForbiddenInApi);

        rule.Check(Architecture);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Orders")]
    [InlineData("Settlements")]
    public void Contracts_DoNotDependOnOtherModuleLayers(string module)
    {
        var rule = AssemblyMustNotDependOn($"Shop.{module}.Contracts", ForbiddenInContracts);

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public void DataHub_DoesNotDependOnOtherModulesInternals()
    {
        var rule = NamespaceMustNotDependOn(DataHubNamespace, ForbiddenInDataHub);

        rule.Check(Architecture);
    }

    // Testy kontrolne: te same reguły zastosowane do Shop.Orders.Infrastructure muszą się wywrócić.
    // Gdyby wzorzec niczego nie dopasowywał, reguły powyżej przechodziłyby niezależnie od kodu.
    public static TheoryData<string> ForbiddenPatternsViolatedByOrdersInfrastructure() => new()
    {
        ForbiddenInDomain,
        ForbiddenInApi,
        ForbiddenInApplication,
        ForbiddenInContracts,
        ForbiddenInDataHub
    };

    [Theory]
    [Trait("AC", "AC-2")]
    [MemberData(nameof(ForbiddenPatternsViolatedByOrdersInfrastructure))]
    public void AssemblyRule_WhenAppliedToOrdersInfrastructure_Fails(string forbiddenNamespacePattern)
    {
        var rule = AssemblyMustNotDependOn(OrdersInfrastructure, forbiddenNamespacePattern);

        Should.Throw<FailedArchRuleException>(() => rule.Check(Architecture));
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public void NamespaceRule_WhenAppliedToOrdersModule_Fails()
    {
        var rule = NamespaceMustNotDependOn(@"^Shop\.Orders(\..*)?$", ForbiddenInDataHub);

        Should.Throw<FailedArchRuleException>(() => rule.Check(Architecture));
    }

    private static IArchRule AssemblyMustNotDependOn(string assemblyName, string forbiddenNamespacePattern) =>
        Types().That().ResideInAssembly(Assembly.Load(assemblyName))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbiddenNamespacePattern)
            .WithoutRequiringPositiveResults();

    private static IArchRule NamespaceMustNotDependOn(string namespacePattern, string forbiddenNamespacePattern) =>
        Types().That().ResideInNamespaceMatching(namespacePattern)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbiddenNamespacePattern)
            .WithoutRequiringPositiveResults();

    private static string OtherModulesDomainOrApplication(string module)
    {
        var otherModules = string.Join("|", new[] { "Orders", "Settlements", "DataHub" }.Where(other => other != module));
        return $@"^Shop\.({otherModules})\.(Domain|Application)(\..*)?$";
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
