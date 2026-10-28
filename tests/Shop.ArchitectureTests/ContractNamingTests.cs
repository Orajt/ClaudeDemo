using System.Reflection;

namespace Shop.ArchitectureTests;

public sealed class ContractNamingTests
{
    private const string IntegrationEventSuffix = "IntegrationEvent";

    [Theory]
    [Trait("AC", "AC-18")]
    [InlineData("Shop.Orders.Contracts")]
    [InlineData("Shop.Settlements.Contracts")]
    public void IntegrationEvents_HaveIntegrationEventSuffix(string contractsAssemblyName)
    {
        var contractsAssembly = Assembly.Load(contractsAssemblyName);

        // Enumy i struktury to typy wartości używane przez zdarzenia (reguła contracts.md) – nazwy zdarzeń dotyczą klas i rekordów.
        var misnamed = contractsAssembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.Name.EndsWith(IntegrationEventSuffix, StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        misnamed.ShouldBeEmpty($"Typy w {contractsAssemblyName} bez sufiksu {IntegrationEventSuffix}: {string.Join(", ", misnamed)}");
    }
}
