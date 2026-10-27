using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Spike.DataHub;
using Spike.Orders;
using Spike.Settlements;
using Weasel.Core.Migrations;
using Wolverine.Runtime;

namespace Spike.Tests;

public sealed class SchemaTests(SpikeFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task H1_two_marten_stores_share_envelope_tables_in_wolverine_schema_without_main_AddMarten()
    {
        var schemasWithEnvelopeTables = await fixture.Db.ListAsync<string>(
            "select distinct table_schema from information_schema.tables where table_name like 'wolverine%' order by 1");
        var schemasWithEventTables = await fixture.Db.ListAsync<string>(
            "select table_schema from information_schema.tables where table_name = 'mt_events' order by 1");

        schemasWithEnvelopeTables.ShouldBe(["wolverine"]);
        schemasWithEventTables.ShouldBe(["spike_orders", "spike_settlements"]);

        // Brak głównego store'a Martena – głównym magazynem wiadomości jest PersistMessagesWithPostgresql.
        fixture.Host.Services.GetService<IDocumentStore>().ShouldBeNull();
        fixture.Host.Services.GetRequiredService<IWolverineRuntime>().Storage.ShouldNotBeNull();
    }

    [Fact]
    public async Task H4_schema_built_only_by_flyway_has_no_differences_with_marten_wolverine_and_ef_configuration()
    {
        var services = fixture.Host.Services;

        await services.GetRequiredService<IOrdersStore>().Storage.Database.AssertDatabaseMatchesConfigurationAsync(CancellationToken);
        await services.GetRequiredService<ISettlementsStore>().Storage.Database.AssertDatabaseMatchesConfigurationAsync(CancellationToken);

        var messageStore = services.GetRequiredService<IWolverineRuntime>().Storage;
        var messageDatabase = messageStore.ShouldBeAssignableTo<IDatabase>()!;
        await messageDatabase.AssertDatabaseMatchesConfigurationAsync(CancellationToken);

        using var scope = services.CreateScope();
        var dataHub = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
        (await dataHub.Database.GetPendingMigrationsAsync(CancellationToken)).ShouldBeEmpty();
        dataHub.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
