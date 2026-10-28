using Microsoft.Extensions.DependencyInjection;
using Shop.Orders.Infrastructure;
using Shop.Settlements.Infrastructure;
using Weasel.Core.Migrations;
using Wolverine.Runtime;

namespace Shop.IntegrationTests;

/// <summary>
/// Baza zbudowana wyłącznie przez Flyway ma być zgodna z konfiguracją Martena i Wolverine – to samo co komenda <c>db-assert</c>.
/// Test łapie zapomnianą migrację. Część EF (HasPendingModelChanges) dojdzie w etapie 5, gdy powstanie DataHubDbContext.
/// </summary>
public sealed class SchemaConsistencyTests(ShopFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-15")]
    public async Task OrdersStore_MatchesDatabaseBuiltByFlyway()
    {
        var store = fixture.Host.Services.GetRequiredService<IOrdersStore>();

        await store.Storage.Database.AssertDatabaseMatchesConfigurationAsync(CancellationToken);
    }

    [Fact]
    [Trait("AC", "AC-15")]
    public async Task SettlementsStore_MatchesDatabaseBuiltByFlyway()
    {
        var store = fixture.Host.Services.GetRequiredService<ISettlementsStore>();

        await store.Storage.Database.AssertDatabaseMatchesConfigurationAsync(CancellationToken);
    }

    [Fact]
    [Trait("AC", "AC-15")]
    public async Task MessageStorage_MatchesDatabaseBuiltByFlyway()
    {
        var messageStore = fixture.Host.Services.GetRequiredService<IWolverineRuntime>().Storage;

        var messageDatabase = messageStore.ShouldBeAssignableTo<IDatabase>()!;
        await messageDatabase.AssertDatabaseMatchesConfigurationAsync(CancellationToken);
    }
}
