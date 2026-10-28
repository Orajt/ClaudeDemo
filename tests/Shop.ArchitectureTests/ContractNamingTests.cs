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

        var misnamed = FindMisnamedEventTypes(contractsAssembly.GetExportedTypes());

        misnamed.ShouldBeEmpty($"Typy w {contractsAssemblyName} bez sufiksu {IntegrationEventSuffix}: {string.Join(", ", misnamed)}");
    }

    [Fact]
    [Trait("AC", "AC-18")]
    public void FindMisnamedEventTypes_ReportsTopLevelRecordWithoutSuffixButNotNestedDto()
    {
        Type[] types = [typeof(SamplePlacedIntegrationEvent), typeof(SamplePlacedIntegrationEvent.Line), typeof(SamplePlaced), typeof(SampleStatus)];

        var misnamed = FindMisnamedEventTypes(types);

        misnamed.ShouldBe([typeof(SamplePlaced).FullName]);
    }

    // Enumy i struktury to typy wartości używane przez zdarzenia (reguła contracts.md) – nazwy zdarzeń dotyczą klas i rekordów.
    // DTO używane przez zdarzenie (np. pozycja zamówienia) są typami zagnieżdżonymi w zdarzeniu, więc nie wymagają sufiksu.
    private static IReadOnlyList<string?> FindMisnamedEventTypes(IEnumerable<Type> exportedTypes) =>
        exportedTypes
            .Where(type => type.IsClass && !type.IsNested && !type.Name.EndsWith(IntegrationEventSuffix, StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();
}

// Typy syntetyczne dla kontrprzykładu – na poziomie przestrzeni nazw, bo typ zagnieżdżony w klasie testowej byłby pominięty.
internal sealed record SamplePlacedIntegrationEvent(Guid OrderId, IReadOnlyList<SamplePlacedIntegrationEvent.Line> Lines)
{
    internal sealed record Line(string Sku, int Quantity);
}

internal sealed record SamplePlaced(Guid OrderId);

internal enum SampleStatus
{
    Placed
}
