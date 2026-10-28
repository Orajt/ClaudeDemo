namespace Shop.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    [Trait("AC", "AC-2")]
    public void ProjectReferences_FollowLayerAndModuleRules()
    {
        var violations = DependencyRules.FindViolations(ProjectFile.ReadAllFromSources());

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public void DomainReferencingWolverinePackage_IsReportedWithProjectAndPackage()
    {
        var domain = Project("Shop.Orders.Domain", packages: ["WolverineFx"]);

        var violations = DependencyRules.FindViolations([domain]);

        var violation = violations.ShouldHaveSingleItem();
        violation.ShouldContain("Shop.Orders.Domain");
        violation.ShouldContain("WolverineFx");
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Shop.Orders.Domain", "Shop.Orders.Application")]
    [InlineData("Shop.Orders.Contracts", "Shop.SharedKernel")]
    [InlineData("Shop.Orders.Application", "Shop.Orders.Infrastructure")]
    [InlineData("Shop.Orders.Application", "Shop.Settlements.Domain")]
    [InlineData("Shop.Orders.Application", "Shop.Settlements.Application")]
    [InlineData("Shop.Orders.Api", "Shop.Orders.Infrastructure")]
    [InlineData("Shop.DataHub.Application", "Shop.Orders.Domain")]
    [InlineData("Shop.Settlements.Infrastructure", "Shop.Orders.Application")]
    public void ForbiddenProjectReference_IsReported(string from, string to)
    {
        var violations = DependencyRules.FindViolations([Project(from, projects: [to])]);

        violations.ShouldHaveSingleItem().ShouldContain(to);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Shop.Orders.Api", "Marten")]
    [InlineData("Shop.Orders.Api", "Microsoft.EntityFrameworkCore")]
    [InlineData("Shop.Settlements.Domain", "Microsoft.AspNetCore.Mvc.Core")]
    [InlineData("Shop.Settlements.Domain", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void ForbiddenPackageReference_IsReported(string from, string package)
    {
        var violations = DependencyRules.FindViolations([Project(from, packages: [package])]);

        violations.ShouldHaveSingleItem().ShouldContain(package);
    }

    [Theory]
    [Trait("AC", "AC-2")]
    [InlineData("Shop.Orders.Application", "Shop.Settlements.Contracts")]
    [InlineData("Shop.DataHub.Application", "Shop.Orders.Contracts")]
    [InlineData("Shop.Orders.Application", "Shop.Orders.Domain")]
    [InlineData("Shop.Orders.Api", "Shop.Api.Contracts.Generated")]
    public void AllowedProjectReference_IsNotReported(string from, string to)
    {
        var violations = DependencyRules.FindViolations([Project(from, projects: [to])]);

        violations.ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public void DomainAnalyzerPackage_IsAllowed()
    {
        var domain = Project("Shop.Orders.Domain", packages: ["JasperFx.Events", "JasperFx.Events.SourceGenerator"]);

        DependencyRules.FindViolations([domain]).ShouldBeEmpty();
    }

    private static ProjectFile Project(string name, string[]? projects = null, string[]? packages = null) =>
        new(name, projects ?? [], packages ?? []);
}
